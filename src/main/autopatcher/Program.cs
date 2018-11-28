﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Azurlane
{
    internal enum Mods
    {
        GodMode,
        WeakEnemy,
        GodMode_Damage,
        GodMode_Cooldown,
        GodMode_WeakEnemy,
        GodMode_Damage_Cooldown,
        GodMode_Damage_WeakEnemy,
        GodMode_Damage_Cooldown_WeakEnemy
    }

    internal static class Program
    {
        internal static bool Abort;
        internal static List<string> ListOfLua;
        internal static Dictionary<Mods, bool> ListOfMod;

        private static List<Action> _listOfAction;

        internal static void SetValue(Mods key, bool value) => ListOfMod[key] = value;

        private static void AddLua(string value) => ListOfLua.Add(value);

        private static void CheckDependencies()
        {
            var missingCount = 0;
            var pythonVersion = 0.0;

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "python";
                    process.StartInfo.Arguments = "--version";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;

                    process.Start();
                    var result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (result.Contains("Python"))
                        pythonVersion = Convert.ToDouble(result.Split(' ')[1].Remove(3));
                    else pythonVersion = -0.0;
                }
            }
            catch
            {
                // Empty
            }

            if (pythonVersion.Equals(0.0) || pythonVersion.Equals(-0.0))
            {
                Utils.LogDebug("No python detected", true, true);
                Utils.LogInfo(Properties.Resources.SolutionPythonMessage, true, true);
                missingCount++;
            }
            else if (pythonVersion < 3.7)
            {
                Utils.LogDebug("Detected Python version {0}.x - expected 3.7.x or newer", true, true, pythonVersion);
                Utils.LogInfo(Properties.Resources.SolutionPythonMessage, true, true);
                missingCount++;
            }

            if (!Directory.Exists(PathMgr.Thirdparty("ljd")))
            {
                Utils.LogDebug(Properties.Resources.LuajitNotFoundMessage, true, true);
                Utils.LogInfo(Properties.Resources.SolutionReferMessage, true, true);
                missingCount++;
            }

            if (!Directory.Exists(PathMgr.Thirdparty("luajit")))
            {
                Utils.LogDebug(Properties.Resources.LjdNotFoundMessage, true, true);
                Utils.LogInfo(Properties.Resources.SolutionReferMessage, true, true);
                missingCount++;
            }

            if (!Directory.Exists(PathMgr.Thirdparty("unityex")))
            {
                Utils.LogDebug(Properties.Resources.UnityExNotFoundMessage, true, true);
                Utils.LogInfo(Properties.Resources.SolutionReferMessage, true, true);
                missingCount++;
            }

            if (missingCount > 0)
                Abort = true;
        }

        private static void CheckVersion()
        {
            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    var latestStatus = wc.DownloadString(Properties.Resources.AutopatcherStatus);
                    if (latestStatus != "ok")
                    {
                        Abort = true;
                        return;
                    }

                    var latestVersion = wc.DownloadString(Properties.Resources.AutopatcherVersion);
                    if ((string)ConfigMgr.GetValue(ConfigMgr.Key.Version) != latestVersion)
                    {
                        Utils.Write("[Obsolete Autopatcher version]", true, true);
                        Utils.Write("Download the latest version from:", true, true);
                        Utils.Write(Properties.Resources.Repository, true, true);
                        Abort = true;
                    }
                }
            }
            catch
            {
                Abort = true;
            }
        }

        private static void Clean(string fileName)
        {
            try
            {
                if (File.Exists(PathMgr.Temp(fileName))) File.Delete(PathMgr.Temp(fileName));
                if (Directory.Exists(PathMgr.Lua(fileName).Replace("\\CAB-android", ""))) Utils.Rmdir(PathMgr.Lua(fileName).Replace("\\CAB-android", ""));

                foreach (var mod in ListOfMod.Keys)
                {
                    var modName = "scripts-" + mod.ToString().ToLower().Replace("_", "-");
                    if (File.Exists(PathMgr.Temp(modName))) File.Delete(PathMgr.Temp(modName));
                    if (Directory.Exists(PathMgr.Lua(modName).Replace("\\CAB-android", ""))) Utils.Rmdir(PathMgr.Lua(modName).Replace("\\CAB-android", ""));
                }
            }
            catch (Exception e)
            {
                Utils.LogException("Exception detected during cleaning", e);
            }
        }

        private static bool GetValue(Mods key) => ListOfMod[key];

        private static void Initialize()
        {
            if (ListOfMod == null)
                ListOfMod = new Dictionary<Mods, bool>();

            foreach (Mods mod in Enum.GetValues(typeof(Mods)))
                ListOfMod.Add(mod, false);

            if (ListOfLua == null)
                ListOfLua = new List<string>();

            ConfigMgr.Initialize();
            Message();
            CheckVersion();
            CheckDependencies();

            AddLua(Properties.Resources.Aircraft);
            AddLua(Properties.Resources.Enemy);

            if (GetValue(Mods.GodMode_Damage) || GetValue(Mods.GodMode_Cooldown) || GetValue(Mods.GodMode_Damage_Cooldown) ||
                GetValue(Mods.GodMode_Damage_WeakEnemy) || GetValue(Mods.GodMode_Damage_Cooldown_WeakEnemy))
            {
                AddLua(Properties.Resources.Weapon);
            }

            if ((bool)ConfigMgr.GetValue(ConfigMgr.Key.Replace_Skin))
                AddLua(Properties.Resources.Ship);

            if ((bool)ConfigMgr.GetValue(ConfigMgr.Key.Remove_Skill))
                AddLua(Properties.Resources.EnemySkill);
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Initialize();
            if (Abort)
                return;

            if (args.Length < 1)
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = @"Open an AssetBundle...";
                    dialog.Filter = @"Azurlane AssetBundle|scripts*";
                    dialog.CheckFileExists = true;
                    dialog.Multiselect = false;
                    dialog.ShowDialog();

                    if (File.Exists(dialog.FileName))
                    {
                        args = new[] { dialog.FileName };
                    }
                    else
                    {
                        Utils.Write(@"Please open an AssetBundle...", true, true);
                        goto END;
                    }
                }
            }
            else if (args.Length > 1)
            {
                Utils.Write(@"Invalid argument, usage: Azurlane.exe <path-to-assetbundle>", true, true);
                goto END;
            }

            var filePath = Path.GetFullPath(args[0]);
            var fileDirectoryPath = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);

            if (!File.Exists(filePath))
            {
                Utils.Write(Directory.Exists(fileDirectoryPath) ? $"{args[0]} is a directory, please input a file..." : $"{args[0]} does not exists...", true, true);
                goto END;
            }

            if (!AssetBundleMgr.CheckAssetBundle(filePath))
            {
                Utils.Write("Not a valid AssetBundle file...", true, true);
                goto END;
            }

            Clean(fileName);

            if (!Directory.Exists(PathMgr.Temp()))
                Directory.CreateDirectory(PathMgr.Temp());

            var index = 1;
            if (_listOfAction == null)
            {
                _listOfAction = new List<Action>()
                {
                    () =>
                    {
                        try {
                            Utils.LogInfo("Copying AssetBundle to tmp workspace...", true, false);
                            File.Copy(filePath, PathMgr.Temp(fileName), true);
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during copying AssetBundle to tmp workspace", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Decrypting AssetBundle...", true, false);
                            Utils.Command($"Azcli.exe --dev5 \"{PathMgr.Temp(fileName)}\"");
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during decrypting AssetBundle", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Unpacking AssetBundle...", true, false);
                            Utils.Command($"Azcli.exe --dev7 \"{PathMgr.Temp(fileName)}\"");
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during unpacking AssetBundle", e);
                        }
                    },
                    () =>
                    {
                        try {
                            var showDoneMessage = true;
                            Utils.LogInfo("Decrypting Lua...", true, false);
                            foreach (var lua in ListOfLua) {
                                Utils.Command($"Azcli.exe --dev1 \"{PathMgr.Lua(fileName, lua)}\"");

                                if (LuaMgr.CheckLuaState(PathMgr.Lua(fileName, lua)) != LuaMgr.State.Encrypted)
                                    break;

                                Console.WriteLine();
                                Utils.LogDebug($"Failed to decrypt {Path.GetFileName(lua)}", true, true);
                                showDoneMessage = false;
                            }
                            if (showDoneMessage)
                                Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during decrypting Lua", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Decompiling Lua...", true, false);
                            foreach (var lua in ListOfLua) {
                                Utils.Write($@" {index}/{ListOfLua.Count}", false, false);
                                Utils.Command($"Azcli.exe --dev3 \"{PathMgr.Lua(fileName, lua)}\"");
                                index++;
                            }
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during decompiling Lua", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Cloning Lua & AssetBundle", true, false);
                            foreach (var mod in ListOfMod)
                            {
                                if (!mod.Value)
                                    break;

                                var modName = ("scripts-" + mod.Key).ToLower().Replace("_", "-");

                                if (!Directory.Exists(PathMgr.Lua(modName)))
                                    Directory.CreateDirectory(PathMgr.Lua(modName));

                                foreach (var lua in ListOfLua)
                                    File.Copy(PathMgr.Lua(fileName, lua), PathMgr.Lua(modName, lua), true);

                                File.Copy(PathMgr.Temp(fileName), PathMgr.Temp(modName), true);
                            }
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during cloning lua & assetbundle", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Cleaning...", true, false);
                            if (File.Exists(PathMgr.Temp(fileName))) File.Delete(PathMgr.Temp(fileName));
                            if (Directory.Exists(PathMgr.Lua(fileName).Replace("\\CAB-android", ""))) Utils.Rmdir(PathMgr.Lua(fileName).Replace("\\CAB-android", ""));
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during cleaning", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Rewriting Lua...", true, false);
                            Utils.Command("Rewriter.exe");
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during rewriting Lua", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Recompiling Lua...", true, false);
                            foreach (var mod in ListOfMod)
                            {
                                if (!mod.Value)
                                    break;

                                var modName = ("scripts-" + mod.Key).ToLower().Replace("_", "-");
                                foreach (var lua in ListOfLua)
                                    Utils.Command($"Azcli.exe --dev4 \"{PathMgr.Lua(modName, lua)}\"");
                            }
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during recompiling Lua", e);
                        }
                    },
                    () =>
                    {
                        try {
                            var showDoneMessage = true;
                            Utils.LogInfo("Encrypting Lua...", true, false);
                            foreach (var mod in ListOfMod)
                            {
                                if (!mod.Value)
                                    break;

                                var modName = ("scripts-" + mod.Key).ToLower().Replace("_", "-");

                                foreach (var lua in ListOfLua) {
                                    Utils.Command($"Azcli.exe --dev2 \"{PathMgr.Lua(modName, lua)}\"");

                                    if (LuaMgr.CheckLuaState(PathMgr.Lua(modName, lua)) != LuaMgr.State.Decrypted)
                                        break;

                                    Console.WriteLine();
                                    Utils.LogDebug($"Failed to encrypt {mod}/{Path.GetFileName(lua)}...", true, true);
                                    showDoneMessage = false;
                                }
                            }
                            if (showDoneMessage)
                                Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during encrypting Lua", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Repacking AssetBundle...", true, false);
                            foreach (var mod in ListOfMod)
                            {
                                if (!mod.Value)
                                    break;

                                var modName = ("scripts-" + mod.Key).ToLower().Replace("_", "-");

                                Utils.Write($@" {index}/{ListOfMod.Count(x => x.Value)}", false, false);
                                Utils.Command($"Azcli.exe --dev8 \"{PathMgr.Temp(modName)}\"");
                                index++;
                            }
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during repacking AssetBundle", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Encrypting AssetBundle...", true, false);
                            foreach (var mod in ListOfMod)
                            {
                                if (!mod.Value)
                                    break;

                                var modName = ("scripts-" + mod.Key).ToLower().Replace("_", "-");
                                Utils.Command($"Azcli.exe --dev6 \"{PathMgr.Temp(modName)}\"");
                            }
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during encrypting AssetBundle", e);
                        }
                    },
                    () =>
                    {
                        try {
                            Utils.LogInfo("Copying modified AssetBundle to original location...", true, false);
                            foreach (var mod in ListOfMod)
                            {
                                if (!mod.Value)
                                    break;

                                var modName = ("scripts-" + mod.Key).ToLower().Replace("_", "-");

                                if (File.Exists(Path.Combine(fileDirectoryPath, modName)))
                                    File.Delete(Path.Combine(fileDirectoryPath, modName));

                                File.Copy(PathMgr.Temp(modName), Path.Combine(fileDirectoryPath, modName));
                            }
                            Utils.Write(" <done>", false, true);
                        }
                        catch (Exception e)
                        {
                            Utils.Write(" <failed>", false, true);
                            Utils.LogException("Exception detected during copying modified AssetBundle to original location", e);
                        }
                    }
                };
            }

            try
            {
                foreach (var action in _listOfAction)
                {
                    if (Abort)
                        break;
                    index = 1;
                    action.Invoke();
                }
            }
            finally
            {
                Utils.LogInfo("Cleaning...", true, true);
                Clean(fileName);

                Console.WriteLine();
                Utils.Write("Finished.", true, true);
            }

        END:
            Utils.Write("Press any key to exit...", true, true);
            Console.ReadKey();
        }

        private static void Message()
        {
            Utils.Write("", true, true);
            Utils.Write("Azurlane Autopatcher", true, true);
            Utils.Write("Version {0}", true, true, ConfigMgr.GetValue(ConfigMgr.Key.Version));
            Utils.Write("{0}", true, true, Properties.Resources.Author);
            Utils.Write("", true, true);
        }
    }
}