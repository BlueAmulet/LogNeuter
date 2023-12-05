﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogNeuter
{
    [BepInPlugin(ID, Name, Version)]
    public class LogNeuter : BaseUnityPlugin
    {
        internal const string Name = "LogNeuter";
        internal const string Author = "BlueAmulet";
        internal const string ID = Author + "." + Name;
        internal const string Version = "1.0.0";

        private static ManualLogSource Log;
        private static ConfigFile ConfigFile;

        // Configuration
        private static ConfigEntry<int> version;
        private static ConfigEntry<bool> fixSpatializer;
        private static ConfigEntry<bool> genBlockAll;
        private static bool allowSave = false;
        private static bool warnSave = true;

        // Patching stuff
        private static readonly Harmony harmony = new Harmony(ID);
        private static readonly HarmonyMethod transpiler = new HarmonyMethod(AccessTools.Method(typeof(LogNeuter), nameof(Transpiler)));
        private static readonly Dictionary<string, HashSet<string>> staticLogs = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, List<Regex>> dynamicLogs = new Dictionary<string, List<Regex>>();

        // Generation stuff
        private static StreamWriter genFile;

        public void Awake()
        {
            Log = Logger;
            ConfigFile = Config;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Never save our config
            MethodBase saveMethod = AccessTools.Method(typeof(ConfigFile), nameof(ConfigFile.Save));
            HarmonyMethod savePatch = new HarmonyMethod(AccessTools.Method(typeof(LogNeuter), nameof(PrefixConfigSave)));
            harmony.Patch(saveMethod, prefix: savePatch);

            // Configuration
            warnSave = false;
            fixSpatializer = Config.Bind("Config", "FixSpatializer", true, "Disable broken spatialize on audio sources");
            genBlockAll = Config.Bind("Config", "GenBlockAll", false, "Generate a config file that blocks all logging");
            warnSave = true;

            // Scan all game classes for logging
            if (genBlockAll.Value)
            {
                const string GenID = ID + ".Generated";
                genFile = new StreamWriter(Path.Combine(Path.GetDirectoryName(Config.ConfigFilePath), GenID + ".cfg"), false, Encoding.UTF8);
                genFile.WriteLine("## This is a generated file, it does not actively do anything and only serves as reference for the real config file");
                Assembly game = Assembly.GetAssembly(typeof(HUDManager));
                HarmonyMethod transpilerGen = new HarmonyMethod(AccessTools.Method(typeof(LogNeuter), nameof(TranspilerGen)));
                foreach (Type type in game.GetTypes())
                {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        try
                        {
                            harmony.Patch(method, transpiler: transpilerGen);
                        }
                        catch
                        {
                        }
                    }
                }
                genFile.Close();
            }

            // Gather config entries
            if (File.Exists(Config.ConfigFilePath))
            {
                MethodBase patchMethod = null;
                string section = null;
                foreach (string text in File.ReadLines(Config.ConfigFilePath))
                {
                    string line = text.Trim();
                    if (line.Length != 0 && !line.StartsWith("#"))
                    {
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            // Handle previous method
                            PatchMethod(patchMethod);

                            // Reset state
                            patchMethod = null;
                            staticLogs.Clear();

                            // Parse section
                            section = line.Substring(1, line.Length - 2);
                            if (section.Contains("|"))
                            {
                                string[] split = section.Split(new char[] { '|' }, 2);
                                if (split.Length == 2)
                                {
                                    string typeStr = split[0];
                                    if (!typeStr.Contains(","))
                                    {
                                        typeStr += ", Assembly-CSharp";
                                    }
                                    Type patchType = Type.GetType(typeStr);
                                    if (patchType == null)
                                    {
                                        Logger.LogWarning($"Could not find type: {split[0]}");
                                    }
                                    else
                                    {
                                        patchMethod = AccessTools.Method(patchType, split[1]);
                                        if (patchMethod == null)
                                        {
                                            Logger.LogWarning($"Could not find method: {section}");
                                        }
                                    }
                                }
                                else
                                {
                                    Logger.LogWarning($"Unknown entry: {section}");
                                }
                            }
                        }
                        else if (section != null)
                        {
                            if (line.StartsWith("^") && line.EndsWith("$"))
                            {
                                if (!dynamicLogs.ContainsKey(section))
                                {
                                    dynamicLogs[section] = new List<Regex>();
                                }
                                dynamicLogs[section].Add(new Regex(line, RegexOptions.Compiled));
                            }
                            else
                            {
                                if (!staticLogs.ContainsKey(section))
                                {
                                    staticLogs[section] = new HashSet<string>();
                                }
                                staticLogs[section].Add(line);
                            }
                        }
                    }
                }

                // Handle last section
                PatchMethod(patchMethod);
            }
            else
            {
                allowSave = true;
                Config.Save();
                allowSave = false;
            }

            // Patch log
            if (!genBlockAll.Value)
            {
                int patchedMethods = 0;
                foreach (MethodBase method in harmony.GetPatchedMethods())
                {
                    Logger.LogInfo("Patched " + method.DeclaringType.Name + "." + method.Name);
                    patchedMethods++;
                }
                Logger.LogInfo(patchedMethods + " patches applied");
            }
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Game has no valid spatializer, disable spatialization
            if (fixSpatializer.Value)
            {
                int fixedSpatilization = 0;
                foreach (AudioSource source in Resources.FindObjectsOfTypeAll<AudioSource>())
                {
                    if (source.spatialize)
                    {
                        source.spatialize = false;
                        fixedSpatilization++;
                    }
                }
                Logger.LogInfo($"Fixed {fixedSpatilization} spatilization settings");
            }
        }

        public static bool PrefixConfigSave(ref ConfigFile __instance)
        {
            // Prevent saving our config
            if (__instance == ConfigFile && !allowSave)
            {
                if (warnSave)
                {
                    Log.LogWarning("Saving " + Name + " config blocked for data loss prevention");
                }
                return false;
            }
            else
            {
                return true;
            }
        }

        private static void PatchMethod(MethodBase patchMethod)
        {
            if (patchMethod != null)
            {
                string section = patchMethod.DeclaringType.FullName + "|" + patchMethod.Name;
                if (staticLogs.ContainsKey(section))
                {
                    harmony.Patch(patchMethod, transpiler: transpiler);
                }
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            string section = original.DeclaringType.FullName + "|" + original.Name;
            bool patched = false;
            List<CodeInstruction> instrs = new List<CodeInstruction>(instructions);
            for (int i = 1; i < instrs.Count; i++)
            {
                CodeInstruction instr = instrs[i];
                // Find Debug.Log
                if (instr.opcode == OpCodes.Call && instr.operand is MethodInfo method)
                {
                    // Useless comment to stop Roslynator from trying to merge
                    if (method.IsStatic && method.DeclaringType == typeof(Debug) && method.Name.StartsWith("Log"))
                    {
                        CodeInstruction prevInstr = instrs[i - 1];
                        if (prevInstr.opcode == OpCodes.Ldstr)
                        {
                            // Check if static log should be blocked
                            if (staticLogs.ContainsKey(section) && staticLogs[section].Contains((string)prevInstr.operand))
                            {
                                // TODO: Remove ldstr + call instead
                                instrs[i] = new CodeInstruction(OpCodes.Pop);
                                patched = true;
                            }
                        }
                        else if (dynamicLogs.ContainsKey(section))
                        {
                            // Replace call to us to regex filter
                            string redirect = method.Name + method.GetParameters().Length;
                            MethodInfo redirectMethod = AccessTools.Method(typeof(LogNeuter), redirect);
                            if (redirectMethod != null)
                            {
                                instrs[i] = new CodeInstruction(OpCodes.Call, redirectMethod);
                                instrs.Insert(i, new CodeInstruction(OpCodes.Ldstr, section));
                                patched = true;
                            }
                        }
                    }
                }
            }
            if (!patched)
            {
                Log.LogWarning($"Made no changes to method: {section}");
            }
            return instrs;
        }

        private static void GenWriteEntry(ref bool wroteHeader, string section, HashSet<string> strings, string log)
        {
            if (!strings.Contains(log))
            {
                strings.Add(log);
                if (!wroteHeader)
                {
                    genFile.WriteLine();
                    genFile.WriteLine($"[{section}]");
                    wroteHeader = true;
                }
                if (!log.Contains("="))
                {
                    genFile.WriteLine(log);
                }
            }
        }

        public static IEnumerable<CodeInstruction> TranspilerGen(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> instrs = new List<CodeInstruction>(instructions);
            // Transpilers get recalled for patches
            if (genFile.BaseStream?.CanWrite == true)
            {
                string section = original.DeclaringType.FullName + "|" + original.Name;
                Log.LogInfo($"Scanning {section}");
                bool wroteHeader = false;
                HashSet<string> strings = new HashSet<string>();
                for (int i = 1; i < instrs.Count; i++)
                {
                    CodeInstruction instr = instrs[i];
                    // Find Debug.Log
                    if (instr.opcode == OpCodes.Call && instr.operand is MethodInfo method)
                    {
                        // Useless comment to stop Roslynator from trying to merge
                        if (method.IsStatic && method.DeclaringType == typeof(Debug) && method.Name.StartsWith("Log"))
                        {
                            CodeInstruction prevInstr = instrs[i - 1];
                            if (prevInstr.opcode == OpCodes.Ldstr)
                            {
                                GenWriteEntry(ref wroteHeader, section, strings, (string)prevInstr.operand);
                            }
                            else if (prevInstr.opcode == OpCodes.Call && prevInstr.operand is MethodInfo prevMethod)
                            {
                                // Check if this is String.Format
                                if (prevMethod.IsStatic && prevMethod.DeclaringType == typeof(string) && prevMethod.Name == "Format")
                                {
                                    ParameterInfo[] parameters = prevMethod.GetParameters();
                                    if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string))
                                    {
                                        // TODO: Walk backwards proper
                                        int j = i;
                                        do
                                        {
                                            j--;
                                            //Log.LogError($"{instrs[j]} [{instrs[j].opcode.StackBehaviourPop}, {instrs[j].opcode.StackBehaviourPush}]");
                                        } while ((instrs[j].opcode != OpCodes.Ldstr || !((string)instrs[j].operand).Contains("{")) && j > 0);
                                        //Log.LogError($"Walked {i - j} [{parameters.Length}]\n");
                                        CodeInstruction testInstr = instrs[j];
                                        if (testInstr.opcode == OpCodes.Ldstr)
                                        {
                                            string log = (string)testInstr.operand;
                                            // Sanity check
                                            if (log.Contains("{") && log.Contains("}"))
                                            {
                                                // TODO: Safe?
                                                string escaped = Regex.Escape(log);
                                                escaped = escaped.Replace("\\ ", " ");
                                                escaped = Regex.Replace(escaped, @"\\{[0-9]+}", ".*?");
                                                GenWriteEntry(ref wroteHeader, section, strings, $"^{escaped}$");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return instrs;
        }

        private static bool MatchAny(string section, string log)
        {
            if (!dynamicLogs.ContainsKey(section))
            {
                return false;
            }
            foreach (Regex regex in dynamicLogs[section])
            {
                if (regex.IsMatch(log))
                {
                    return true;
                }
            }
            return false;
        }

        public static void Log1(object message, string section)
        {
            if (!MatchAny(section, message.ToString()))
            {
                Debug.Log(message);
            }
        }

        public static void LogWarning1(object message, string section)
        {
            if (!MatchAny(section, message.ToString()))
            {
                Debug.LogWarning(message);
            }
        }

        public static void LogWarning2(object message, UnityEngine.Object context, string section)
        {
            if (!MatchAny(section, message.ToString()))
            {
                Debug.LogWarning(message, context);
            }
        }

        public static void LogError1(object message, string section)
        {
            if (!MatchAny(section, message.ToString()))
            {
                Debug.LogError(message);
            }
        }

        public static void LogError2(object message, UnityEngine.Object context, string section)
        {
            if (!MatchAny(section, message.ToString()))
            {
                Debug.LogError(message, context);
            }
        }
    }
}
