﻿using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Interface.Utility.Table;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using static Lumina.Data.Parsing.Layer.LayerCommon;
using Task = System.Threading.Tasks.Task;

//shoutout anna clemens

namespace OofPlugin
{
    public sealed class OofPlugin : IDalamudPlugin
    {
        public string Name => "OOF";

        private const string oofCommand = "/oof";
        private const string oofSettings = "/oofsettings";
        private const string oofVideo = "/oofvideo";

        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static ICondition Condition { get; private set; } = null!;
        [PluginService] public static IPartyList PartyList { get; private set; } = null!;
        [PluginService] public static IPluginLog PluginLog { get; set; } = null!;
        //[PluginService] public static ObjectTable ObjectTable { get; private set; } = null!;

        private IDalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private Configuration Configuration { get; init; }
        private PluginUI PluginUi { get; init; }
        private OofHelpers OofHelpers { get; init; }

        // i love global variables!!!! the more global the more globaly it gets
        // sound
        public bool isSoundPlaying { get; set; } = false;
        private DirectSoundOut? soundOut;
        private string? soundFile { get; set; }

        //check for fall
        private float prevPos { get; set; } = 0;
        private float prevVel { get; set; } = 0;
        private float distJump { get; set; } = 0;
        private bool wasJumping { get; set; } = false;
        private bool didPlayerExist = false;
        private Vector3 PlayerPositionCache = new(0, 0, 0);

        //public class DeadPlayer
        //{
        //    public uint PlayerId;
        //    public bool DidPlayOof = false;
        //    public float Distance = 0;
        //}
        //public List<DeadPlayer> DeadPlayers { get; set; } = new List<DeadPlayer>();

        public CancellationTokenSource CancelToken;

        public OofPlugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            Service.Initialize(PluginInterface);
            PluginUi = new PluginUI(Configuration, this, pluginInterface);
            OofHelpers = new OofHelpers();

            // load audio file. idk if this the best way
            LoadSoundFile();

            CommandManager.AddHandler(oofCommand, new CommandInfo(OnCommand)
            {
                HelpMessage = "play oof sound"
            });
            CommandManager.AddHandler(oofSettings, new CommandInfo(OnCommand)
            {
                HelpMessage = "change oof settings"
            });
            CommandManager.AddHandler(oofVideo, new CommandInfo(OnCommand)
            {
                HelpMessage = "open Hbomberguy video on OOF.mp3"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;
            Framework.Update += FrameworkOnUpdate;

            // lmao
            CancelToken = new CancellationTokenSource();
            Task.Run(() => OofAudioPolling(CancelToken.Token));

        }
        public void LoadSoundFile()
        {
            if (Configuration.DefaultSoundImportPath.Length == 0)
            {
                var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "oof.wav");
                soundFile = path;
                Configuration.DefaultSoundImportPath = soundFile;
            }

            if (Configuration.DoubleKillSoundImportPath.Length == 0)
            {
                var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "doublekill.wav");
                Configuration.DoubleKillSoundImportPath = path;
            }

            if (Configuration.TripleKillSoundImportPath.Length == 0)
            {
                var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "multikill.wav");
                Configuration.TripleKillSoundImportPath = path;
            }

            if (Configuration.QuadKillSoundImportPath.Length == 0)
            {
                var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "monsterkill.wav");
                Configuration.QuadKillSoundImportPath = path;
            }

            if (Configuration.FiveKillSoundImportPath.Length == 0)
            {
                var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "ultrakill.wav");
                Configuration.FiveKillSoundImportPath = path;
            }

            if (Configuration.TooManyKillsSoundImportPath.Length == 0)
            {
                var path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "HolyShit_F.wav");
                Configuration.TooManyKillsSoundImportPath = path;
            }

            soundFile = Configuration.DefaultSoundImportPath;
        }
        private void OnCommand(string command, string args)
        {
            if (command == oofCommand) PlaySound(CancelToken.Token);
            if (command == oofSettings) PluginUi.SettingsVisible = true;
            if (command == oofVideo) OpenVideo();

        }

        private void DrawUI()
        {
            PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            PluginUi.SettingsVisible = true;
        }
        private void FrameworkOnUpdate(IFramework framework)
        {
            if (ClientState == null || ClientState.LocalPlayer == null) return;
            try
            {
                if (Configuration.OofOnFall) CheckFallen();
                if (Configuration.OofOnDeath) CheckDeath();
            }
            catch (Exception e)
            {
                PluginLog.Error("failed to check for oof condition:", e.Message);
            }

            didPlayerExist = ClientState.LocalPlayer != null;
            if (didPlayerExist)
            {
                PlayerPositionCache.X = ClientState.LocalPlayer!.Position.X;
                PlayerPositionCache.Y = ClientState.LocalPlayer!.Position.Y;
                PlayerPositionCache.Z = ClientState.LocalPlayer!.Position.Z;
            }
        }

        /// <summary>
        /// check if player has died during alliance, party, and self.
        /// this may be the worst if statement chain i have made
        /// </summary>
        private void CheckDeath()
        {
            if (!Configuration.OofOnDeathBattle && Condition[ConditionFlag.InCombat]) return;

            if (PartyList != null && PartyList.Any())
            {
                if (Configuration.OofOnDeathAlliance && PartyList.Length == 8 && PartyList.GetAllianceMemberAddress(0) != IntPtr.Zero) // the worst "is alliance" check
                {
                    try
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            var allianceMemberAddress = PartyList.GetAllianceMemberAddress(i);
                            if (allianceMemberAddress == IntPtr.Zero) throw new NullReferenceException("allience member address is null");

                            var allianceMember = PartyList.CreateAllianceMemberReference(allianceMemberAddress) ?? throw new NullReferenceException("allience reference is null");
                            OofHelpers.AddRemoveDeadPlayer(allianceMember);
                        }
                    }
                    catch (Exception e)
                    {
                        PluginLog.Error("failed alliance check", e.Message);
                    }
                }
                if (Configuration.OofOnDeathParty)
                {
                    foreach (var member in PartyList)
                    {
                        OofHelpers.AddRemoveDeadPlayer(member, member.Territory.RowId == ClientState!.TerritoryType);
                    }
                }

            }
            else
            {
                if (!Configuration.OofOnDeathSelf) return;
                OofHelpers.AddRemoveDeadPlayer(ClientState!.LocalPlayer!);
            }

        }

        /// <summary>
        /// check if player has taken fall damage (brute force way)
        /// </summary>
        private void CheckFallen()
        {
            // dont run btwn moving areas & also wont work in combat
            if (Condition[ConditionFlag.BetweenAreas]) return;
            if (!Configuration.OofOnFallBattle && Condition[ConditionFlag.InCombat]) return;
            if (!Configuration.OofOnFallMounted && (Condition[ConditionFlag.Mounted] || Condition[ConditionFlag.RidingPillion])) return;

            var isJumping = Condition[ConditionFlag.Jumping];
            var pos = ClientState!.LocalPlayer!.Position.Y;
            var velocity = prevPos - pos;

            if (isJumping && !wasJumping)
            {
                if (prevVel < 0.17) distJump = pos; //started falling
            }
            else if (wasJumping && !isJumping)  // stopped falling
            {
                if (distJump - pos > 9.60) PlaySound(CancelToken.Token); // fell enough to take damage // i guessed and checked this distance value btw
            }

            // set position for next timestep
            prevPos = pos;
            prevVel = velocity;
            wasJumping = isJumping;
        }
        public void StopSound()
        {
            soundOut?.Pause();
            soundOut?.Dispose();

        }
        /// <summary>
        /// Play sound but without referencing windows.forms.
        /// much of the code from: https://github.com/kalilistic/Tippy/blob/5c18d6b21461b0bbe4583a86787ef4a3565e5ce6/src/Tippy/Tippy/Logic/TippyController.cs#L11
        /// </summary>
        /// <param name="token">cancellation token</param>
        /// <param name="volume">optional volume param</param>
        public void PlaySound(CancellationToken token, float volume = 1, string sound = "")
        {
            Task.Run(() =>
            {
                isSoundPlaying = true;
                WaveStream reader;
                try
                {
                    if (sound != string.Empty)
                    {
                        PluginLog.Info(sound);
                        reader = new MediaFoundationReader(sound);
                    }
                    else
                    {
                        reader = new MediaFoundationReader(soundFile);
                    }
                }
                catch (Exception ex)
                {
                    isSoundPlaying = false;
                    PluginLog.Error("Failed read file", ex);
                    return;
                }

                var audioStream = new WaveChannel32(reader)
                {
                    Volume = Configuration.Volume * volume,
                    PadWithZeroes = false // you need this or else playbackstopped event will not fire
                };
                using (reader)
                {
                    if (isSoundPlaying && soundOut != null)
                    {
                        soundOut.Pause();
                        soundOut.Dispose();
                    };
                    //shoutout anna clemens for the winforms fix
                    soundOut = new DirectSoundOut();

                    try
                    {
                        soundOut.Init(audioStream);
                        soundOut.Play();
                        soundOut.PlaybackStopped += OnPlaybackStopped;
                        // run after sound has played. does this work? i have no idea
                        void OnPlaybackStopped(object? sender, StoppedEventArgs e)
                        {
                            soundOut.PlaybackStopped -= OnPlaybackStopped;
                            isSoundPlaying = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        isSoundPlaying = false;
                        PluginLog.Error("Failed play sound", ex);
                        return;
                    }
                }

            }, token);
        }

        /// <summary>
        /// open the hbomberguy video on oof
        /// </summary>
        public static void OpenVideo()
        {
            Util.OpenLink("https://www.youtube.com/watch?v=0twDETh6QaI");
        }

        public void OriginalOofCode(CancellationToken token)
        {
            foreach (var player in OofHelpers.DeadPlayers)
            {
                if (player.DidPlayOof) continue;
                float volume = 1f;
                if (Configuration.DistanceBasedOof && player.Distance != PlayerPositionCache)
                {
                    var dist = 0f;
                    if (player.Distance != Vector3.Zero) dist = Vector3.Distance(PlayerPositionCache, player.Distance);
                    volume = CalcVolumeFromDist(dist);
                }
                PlaySound(token, volume);
                player.DidPlayOof = true;
                break;

            }
        }

        public void CustomOofCode(CancellationToken token) {
            float volume = 1f;
            int oofCount = 0;
            foreach (var player in OofHelpers.DeadPlayers)
            {
                if (player.DidPlayOof) continue;
                if (Configuration.DistanceBasedOof && player.Distance != PlayerPositionCache)
                {
                    var dist = 0f;
                    if (player.Distance != Vector3.Zero) dist = Vector3.Distance(PlayerPositionCache, player.Distance);
                    volume = CalcVolumeFromDist(dist);
                }
                player.DidPlayOof = true;
                oofCount++;
            }
            if (oofCount == 0) return;

            switch (oofCount)
            {
                default:
                    PlaySound(token, volume, Configuration.TooManyKillsSoundImportPath);
                    break;
                case 1:
                    PlaySound(token, volume, Configuration.DefaultSoundImportPath);
                    break;
                case 2:
                    PlaySound(token, volume, Configuration.DoubleKillSoundImportPath);
                    break;
                case 3:
                    PlaySound(token, volume, Configuration.TripleKillSoundImportPath);
                    break;
                case 4:
                    PlaySound(token, volume, Configuration.QuadKillSoundImportPath);
                    break;
                case 5:
                    PlaySound(token, volume, Configuration.FiveKillSoundImportPath);
                    break;
            }
        }

        /// <summary>
        /// check deadPlayers every once in a while. prevents multiple oof from playing too fast
        /// </summary>
        /// <param name="token"> cancellation token</param>
        private async Task OofAudioPolling(CancellationToken token)
        {
            while (true)
            {
                await Task.Delay(200, token);
                if (token.IsCancellationRequested)
                {
                    PluginLog.Warning("Sound loop died?");
                    break;
                }
                if (!OofHelpers.DeadPlayers.Any())
                {
                    continue;
                }
                if (!didPlayerExist) continue;

                CustomOofCode(token);
            }
        }
        public float CalcVolumeFromDist(float dist,float distMax = 30)
        {
            if (dist > distMax) dist = distMax;
            var falloff = Configuration.DistanceFalloff > 0 ? 3f - Configuration.DistanceFalloff*3f : 3f - 0.001f;
            var vol = 1f - ((dist / distMax) *  ( 1 / falloff));
            return Math.Max(Configuration.DistanceMinVolume, vol);
        }

        public async Task TestDistanceAudio(CancellationToken token)
        {
            async Task CheckthenPlay(float volume)
            {
                if (token.IsCancellationRequested) return;
            
                PlaySound(token, volume);
                await Task.Delay(500, token);
            }
            await CheckthenPlay(CalcVolumeFromDist(0));
            await CheckthenPlay(CalcVolumeFromDist(10));
            await CheckthenPlay(CalcVolumeFromDist(20));
            await CheckthenPlay(CalcVolumeFromDist(30));

        }


        /// <summary>
        /// dispose
        /// </summary>
        public void Dispose()
        {
            PluginUi.Dispose();
            CommandManager.RemoveHandler(oofCommand);
            CommandManager.RemoveHandler(oofSettings);
            CommandManager.RemoveHandler(oofVideo);
            CancelToken.Cancel();
            CancelToken.Dispose();

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            PluginInterface.UiBuilder.OpenMainUi -= DrawConfigUI;
            Framework.Update -= FrameworkOnUpdate;
            try
            {
                while (isSoundPlaying)
                {
                    Thread.Sleep(100);
                    soundOut?.Pause();
                    isSoundPlaying = false;

                }
                soundOut?.Dispose();
            }
            catch (Exception e)
            {
                PluginLog.Error("Failed to dispose oofplugin controller", e.Message);
            }


        }


    }
}