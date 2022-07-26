﻿using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using FrooxEngine;
using BaseX;
using CodeX;
using HarmonyLib;
using NeosModLoader;

namespace SVGImporter
{
    public class SVGImporter : NeosMod
    {
        public override string Name => "SVGImporter";
        public override string Author => "dfgHiatus";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/dfgHiatus/https://github.com/dfgHiatus/NeosSVGImporter/";
        public static ModConfiguration config;
        public override void OnEngineInit()
        {
            new Harmony("net.dfgHiatus.SVGImporter").PatchAll();
            config = GetConfiguration();
            Engine.Current.RunPostInit(() => AssetPatch());
        }

        public static void AssetPatch()
        {
            var aExt = Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions");
            aExt.Value[AssetClass.Model].Add("svg");
        }

        [HarmonyPatch(typeof(ModelPreimporter), "Preimport")]
        public class FileImporterPatch
        {
            public static void Postfix(ref string __result, string model, string tempPath)
            {
                var modelName = Path.GetFileNameWithoutExtension(model);
                if (ContainsUnicodeCharacter(modelName))
                {
                    throw new ArgumentException("Imported model cannot have unicode characters in its file name.");
                }

                var normalizedExtension = Path.GetExtension(model).Replace(".", "").ToLower();
                var trueCachePath = Path.Combine(Engine.Current.CachePath, "Cache");
                var time = DateTime.Now.Ticks.ToString();

                if (normalizedExtension == "svg" && BlenderInterface.IsAvailable)
                {
                    var blenderTarget = Path.Combine(trueCachePath, $"{modelName}_v2_{time}.glb").Replace("\'", "/");
                    SVGToGLB(model, blenderTarget);
                    __result = blenderTarget;
                    return;
                }
            }

            private static bool ContainsUnicodeCharacter(string input)
            {
                const int MaxAnsiCode = 255;
                return input.Any(c => c > MaxAnsiCode);
            }

            private static void SVGToGLB(string input, string output)
            {
                // Yes, deleting the default cube is necessary for this script to function
                RunBlenderScript($"import bpy\nbpy.ops.import_curve.svg(filepath = '{input}')\nobjs = bpy.data.objects\ntry:    objs.remove(objs['Cube'], do_unlink = True)\nexcept:    pass\nfor obj in bpy.data.objects:\n    if type(obj.data) == 'Curve':\n        bpy.context.view_layer.objects.active = obj\n        bpy.ops.object.convert(target = 'MESH')\nbpy.ops.export_scene.gltf(filepath = '{output}')");
            }

            private static void RunBlenderScript(string script, string arguments = "-b -P \"{0}\"")
            {
                var tempBlenderScript = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".py");
                File.WriteAllText(tempBlenderScript, script);
                var blenderArgs = string.Format(arguments, tempBlenderScript);
                blenderArgs = "--disable-autoexec " + blenderArgs;

                var process = new Process();
                process.StartInfo.FileName = BlenderInterface.Executable;
                process.StartInfo.Arguments = blenderArgs;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.OutputDataReceived += OnOutput;
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();

                File.Delete(tempBlenderScript);
            }
            private static void OnOutput(object sender, DataReceivedEventArgs e)
            {
                Msg(e.Data);
            }
        }
    }
}