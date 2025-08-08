﻿using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using ImGuiScene;
using System;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Textures;

namespace OofPlugin
{
    partial class PluginUI : IDisposable
    {
        private Configuration configuration;

        private OofPlugin plugin;
        private readonly ISharedImmediateTexture creditsTexture;
        private FileDialogManager manager { get; }
        private bool settingsVisible = false;
        private float fallOptionsHeight = 0;
        private float deathOptionsHeight = 0;
        public bool SettingsVisible
        {
            get { return settingsVisible; }
            set { settingsVisible = value; }
        }
        public PluginUI(Configuration configuration, OofPlugin plugin, IDalamudPluginInterface pluginInterface)
        {
            this.configuration = configuration;
            this.plugin = plugin;
            manager = new FileDialogManager
            {
                AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
            };
            var imagePath = Path.Combine(pluginInterface.AssemblyLocation.Directory?.FullName!, "credits.png");
            this.creditsTexture = Service.TextureProvider.GetFromFile(imagePath)!;
        }

        public void Draw()
        {
            DrawSettingsWindow();
        }

        public string GetSingleDeathSound()
        {
            return configuration.DefaultSoundImportPath;
        }

        public void SaveSingleDeathSound(string path) {
            configuration.DefaultSoundImportPath = path;
        }

        public string GetDoubleKillSound()
        {
            return configuration.DoubleKillSoundImportPath;
        }

        public void SaveDoubleKillSound(string path) {
            configuration.DoubleKillSoundImportPath = path;
        }

        public string GetTripleKillSound()
        {
            return configuration.TripleKillSoundImportPath;
        }

        public void SetTripleKillSound(string path) {
            configuration.TripleKillSoundImportPath = path;
        }

        public string GetQuadKillSound() {
            return configuration.QuadKillSoundImportPath;
        }

        public void SetQuadKillSound(string path) {
            configuration.QuadKillSoundImportPath = path;
        }

        public string GetFiveKillSound()
        {
            return configuration.FiveKillSoundImportPath;
        }

        public void SetFiveKillSound(string path) {
            configuration.FiveKillSoundImportPath = path;
        }

        public string GetWipeKillSound()
        {
            return configuration.TooManyKillsSoundImportPath;
        }

        public void SetWipeKillSound(string path) {
            configuration.TooManyKillsSoundImportPath = path;
        }

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible) return;
            // i miss html/css
            ImGui.SetNextWindowSize(new Vector2(355, 700), ImGuiCond.Appearing);
            if (ImGui.Begin("oof options", ref settingsVisible,
                 ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                

                AddLoadAudioUI(GetSingleDeathSound, SaveSingleDeathSound, "Sound file to play for one death and fall damage");
                AddLoadAudioUI(GetDoubleKillSound, SaveDoubleKillSound, "Sound file to play for two deaths");
                AddLoadAudioUI(GetTripleKillSound, SetTripleKillSound, "Sound file to play for three deaths");
                AddLoadAudioUI(GetQuadKillSound, SetQuadKillSound, "Sound file to play for four deaths");
                AddLoadAudioUI(GetFiveKillSound, SetFiveKillSound, "Sound file to play for five deaths");
                AddLoadAudioUI(GetWipeKillSound, SetWipeKillSound, "Sound file to play for six+ deaths");

                /// volume cntrol -----
                var oofVolume = configuration.Volume;
                var headingColor = ImGuiColors.DalamudGrey;
                ImGui.TextColoredWrapped(headingColor, "Volume");
                ImGui.AlignTextToFramePadding();
                IconTextColor(FontAwesomeIcon.VolumeMute.ToIconString(), headingColor);
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetFontSize() * 1.6f);
                if (ImGui.SliderFloat("###volume", ref oofVolume, 0.0f, 1.0f))
                {
                    configuration.Volume = oofVolume;
                    configuration.Save();
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                IconTextColor(FontAwesomeIcon.VolumeUp.ToIconString(), headingColor);
                /// end volume control -----
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Spacing();

                ImGui.Spacing();

                //ImGuiComponents.HelpMarker(
                //  "turn on/off various conditions to trigger sound");
                ImGui.TextColoredWrapped(ImGuiColors.DalamudGrey, "Play sound on");

                // when self falls options
                var oofOnFall = configuration.OofOnFall;
                SectionStart(fallOptionsHeight);
                SectionHeader("Fall damage (self only)", ref oofOnFall, () => { configuration.OofOnFall = oofOnFall; });
                if (!oofOnFall) ImGui.BeginDisabled();
                ImGui.Columns(2);
                var oofOnFallBattle = configuration.OofOnFallBattle;
                if (ImGui.Checkbox("During combat###fall:combat", ref oofOnFallBattle))
                {
                    configuration.OofOnFallBattle = oofOnFallBattle;
                    configuration.Save();
                }

                ImGui.NextColumn();
                var oofOnFallMounted = configuration.OofOnFallMounted;
                if (ImGui.Checkbox("While mounted###fall:mounted", ref oofOnFallMounted))
                {
                    configuration.OofOnFallMounted = oofOnFallMounted;
                    configuration.Save();
                }

                ImGui.Columns(1);
                if (!oofOnFall) ImGui.EndDisabled();

                SectionEnd(ref fallOptionsHeight, oofOnFall ? ImGuiCol.PopupBg : ImGuiCol.TitleBg);
                ImGui.Spacing();
                // when people die options
                SectionStart(deathOptionsHeight);
                var oofOnDeath = configuration.OofOnDeath;

                SectionHeader("Death", ref oofOnDeath, () => { configuration.OofOnDeath = oofOnDeath; });
                if (!oofOnDeath) ImGui.BeginDisabled();



             
                ImGui.Columns(2);

                var oofInBattle = configuration.OofOnDeathBattle;

                if (ImGui.Checkbox("During combat###death:combat", ref oofInBattle))
                {
                    configuration.OofOnDeathBattle = oofInBattle;
                    configuration.Save();
                }
                ImGui.NextColumn();

                var oofOnDeathSelf = configuration.OofOnDeathSelf;

                if (ImGui.Checkbox("Self dies###death:self", ref oofOnDeathSelf))
                {
                    configuration.OofOnDeathSelf = oofOnDeathSelf;
                    configuration.Save();
                }



                var oofOthersInParty = configuration.OofOnDeathParty;

                if (ImGui.Checkbox("Party member dies###death:party", ref oofOthersInParty))
                {
                    configuration.OofOnDeathParty = oofOthersInParty;
                    configuration.Save();
                }
                var oofOnDeathAlliance = configuration.OofOnDeathAlliance;

                if (ImGui.Checkbox("Alliance member dies###death:alliance", ref oofOnDeathAlliance))
                {
                    configuration.OofOnDeathAlliance = oofOnDeathAlliance;
                    configuration.Save();
                }
                ImGui.Columns(1);
                
                ImGui.Spacing();

                ImGui.Spacing();

                // distance based oof
                ImGui.Spacing();

                var distanceBasedOof = configuration.DistanceBasedOof;
                if (ImGui.Checkbox("Distance Based Oof (DBO)###death:distance", ref distanceBasedOof))
                {
                    configuration.DistanceBasedOof = distanceBasedOof;
                    configuration.Save();
                }
                if (!distanceBasedOof) ImGui.BeginDisabled();

              
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - ImGui.GetFontSize() * 2.4f);
                ImGui.PushFont(UiBuilder.IconFont);

                if (CornerButton(FontAwesomeIcon.Play.ToIconString(), "dbo:play", ImDrawFlags.RoundCornersLeft)) plugin.TestDistanceAudio(plugin.CancelToken.Token);
                ImGui.PopFont();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Test distance");

                ImGui.SameLine(0, 0);
                ImGui.PushFont(UiBuilder.IconFont);

                if (CornerButton(FontAwesomeIcon.Stop.ToIconString(), "dbo:stop", ImDrawFlags.RoundCornersRight)) plugin.StopSound();
                ImGui.PopFont();



                ImGui.TextColoredWrapped(ImGuiColors.DalamudGrey, "Lower volume based on how far someone dies from you, from 0 to 30 yalms");



                /// graph
                var steps = 800;
                var distancePoints = new float[steps];
                var volumemPoints = new float[steps];
                var step = 30f / steps;
                for (int i = 0; i < steps; i++)
                {
                    distancePoints[i] = step * i;
                    volumemPoints[i] = plugin.CalcVolumeFromDist(step * i);

                }
                if (ImPlot.BeginPlot("##dbo:graph", new Vector2(-1, 80), ImPlotFlags.CanvasOnly))
                {
                    ImPlot.PushStyleColor(ImPlotCol.FrameBg, new Vector4(0, 0, 0, 0));
                    ImPlot.PushStyleColor(ImPlotCol.AxisBgHovered, new Vector4(0, 0, 0, 0));
                    ImPlot.PushStyleColor(ImPlotCol.AxisText, ImGuiColors.DalamudGrey);

                    ImPlot.SetupMouseText(ImPlotLocation.North, ImPlotMouseTextFlags.None);
                    ImPlot.SetupLegend(ImPlotLocation.NorthEast, ImPlotLegendFlags.NoHighlightItem);

                    ImPlot.SetupAxisLimitsConstraints(ImAxis.X1, 0, 30);
                    ImPlot.SetupAxisLimitsConstraints(ImAxis.Y1,0, 1);
                    ImPlot.SetupAxisZoomConstraints(ImAxis.X1, 30,30);
                    ImPlot.SetupAxisZoomConstraints(ImAxis.Y1, 1, 1);
                    unsafe { ImPlot.SetupAxes((byte*)IntPtr.Zero, (byte*)IntPtr.Zero, ImPlotAxisFlags.None, ImPlotAxisFlags.NoTickLabels); }
                    ImPlot.PopStyleColor();
                    ImPlot.SetupFinish();
                   
                    ImPlot.PlotLine("volume", ref distancePoints[0], ref volumemPoints[0], steps);

                    ImPlot.EndPlot();
                }
                ImGui.Columns(2);

                ImGui.TextColoredWrapped(headingColor, "Falloff Intensity");
                var distanceFalloff = configuration.DistanceFalloff;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.SliderFloat("###death:distance:falloff", ref distanceFalloff, 0.0f, 1.0f))
                {
                    configuration.DistanceFalloff = distanceFalloff;
                    configuration.Save();
                }

                ImGui.NextColumn();
                ImGui.TextColoredWrapped(headingColor, "Minimum Volume");
                var distanceMinVolume = configuration.DistanceMinVolume;
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X);
                if (ImGui.SliderFloat("###death:distance:volume", ref distanceMinVolume, 0.0f, 1.0f))
                {
                    configuration.DistanceMinVolume = distanceMinVolume;
                    configuration.Save();
                }
                if (!distanceBasedOof) ImGui.EndDisabled();
                ImGui.Columns(1);


                if (!oofOnDeath) ImGui.EndDisabled();

                SectionEnd(ref deathOptionsHeight, oofOnDeath ? ImGuiCol.PopupBg : ImGuiCol.TitleBg);

                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.Spacing();
                /// watch video! --------
                if (ImGuiComponents.IconButtonWithText(FontAwesomeIcon.ExternalLinkSquareAlt, "Watch on Youtube")) OofPlugin.OpenVideo();
                var desc = "Hot Tip: You can Macro the /oofvideo command to\n for easy and streamlined access to this video.";
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(desc);

                ImGui.TextColoredWrapped(ImGuiColors.DalamudGrey, "Learn about the history behind the Roblox Oof with Hbomberguy's Documentary");

               

                ImGui.Spacing();
                ImGui.TextColoredWrapped(ImGuiColors.DalamudGrey, "Original Oof sound by Joey Kuras");

                ImGui.Spacing();


                //logo
                var creditsTextureWrap = this.creditsTexture.GetWrapOrDefault();
                var size = new Vector2(creditsTextureWrap.Width , creditsTextureWrap.Height);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Vector2.Zero);
                ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 13);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                ImGui.SetCursorPos(ImGui.GetWindowSize() - size);

                if (ImGui.ImageButton(creditsTextureWrap.Handle, size)) Util.OpenLink("https://github.com/Frogworks-Interactive");
                ImGui.PopStyleVar(2);
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    ImGui.BeginTooltip();
                    ImGui.Text("Visit Github");

                    ImGui.EndTooltip();
                }

                manager.Draw();
            }
            ImGui.End();
        }




        // Set up the file selector with the right flags and custom side bar items.
        public static FileDialogManager SetupFileManager()
        {
            var fileManager = new FileDialogManager
            {
                AddedWindowFlags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking,
            };

            // Remove Videos and Music.
            fileManager.CustomSideBarItems.Add(("Videos", string.Empty, 0, -1));
            fileManager.CustomSideBarItems.Add(("Music", string.Empty, 0, -1));

            return fileManager;
        }

        /// <summary>
        /// get file name from file path string
        /// </summary>
        /// <returns></returns>
        [GeneratedRegex("[^\\\\]+$")]
        private static partial Regex getFileName();
        public void Dispose()
        {
        }
    }

}
