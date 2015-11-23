﻿using Neptunium.Data;
using Neptunium.MediaSourceStream;
using Neptunium.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;

namespace Neptunium.Media
{
    public static class ShoutcastStationMediaPlayer
    {
        public static bool IsInitialized { get; private set; }

        public static async Task InitializeAsync()
        {
            if (IsInitialized) return;

            BackgroundMediaPlayer.MessageReceivedFromBackground += BackgroundMediaPlayer_MessageReceivedFromBackground;

            //sends a dummy message to get the background audio task started
            var hello = new ValueSet();
            hello.Add(new KeyValuePair<string, object>("Hello", 0));
            BackgroundMediaPlayer.SendMessageToBackground(hello);

            await Task.Delay(1000);

            IsInitialized = true;
        }

        public static void Deinitialize()
        {
            if (!IsInitialized) return;

            BackgroundMediaPlayer.MessageReceivedFromBackground -= BackgroundMediaPlayer_MessageReceivedFromBackground;

            IsInitialized = false;
        }

        private static async void BackgroundMediaPlayer_MessageReceivedFromBackground(object sender, MediaPlayerDataReceivedEventArgs e)
        {
            foreach (var message in e.Data)
            {
                switch(message.Key)
                {
                    case Messages.MetadataChangedMessage:
                        {
                            var mcMessage = JsonHelper.FromJson<MetadataChangedMessage>(message.Value.ToString());

                            try
                            {
                                if (MetadataChanged != null)
                                    MetadataChanged(sender, new ShoutcastMediaSourceStreamMetadataChangedEventArgs(mcMessage.Track, mcMessage.Artist));

                                SongMetadata = new ShoutcastSongInfo() { Track = mcMessage.Track, Artist = mcMessage.Artist };
                            }
                            catch (Exception) { }

                            break;
                        }
                    case Messages.StationInfoMessage:
                        {
                            var siMessage = JsonHelper.FromJson<StationInfoMessage>(message.Value.ToString());


                            if (!StationDataManager.IsInitialized)
                                await StationDataManager.InitializeAsync();

                            if (!string.IsNullOrWhiteSpace(siMessage.CurrentStation))
                            {
                                currentStationModel = StationDataManager.Stations.FirstOrDefault(x => x.Name == siMessage.CurrentStation);

                                if (CurrentStationChanged != null) CurrentStationChanged(null, EventArgs.Empty);
                            }

                            break;
                        }
                    case Messages.BackgroundAudioErrorMessage:
                        {
                            var baeMessage = JsonHelper.FromJson<BackgroundAudioErrorMessage>(message.Value.ToString());

                            //if (BackgroundAudioError != null)
                            //    BackgroundAudioError(null, EventArgs.Empty);

                            break;
                        }
                }
            }
        }

        public static ShoutcastSongInfo SongMetadata { get; private set; }

        private static StationModel currentStationModel = null;
        //private static ShoutcastMediaSourceStream currentStationMSSWrapper = null;

        public static StationModel CurrentStation { get { return currentStationModel; } }

        public static bool IsPlaying
        {
            get
            {
                var state = BackgroundMediaPlayer.Current.CurrentState;

                return state == MediaPlayerState.Opening || state == MediaPlayerState.Playing || state == MediaPlayerState.Buffering;
            }
        }

        //public static ShoutcastStationInfo StationInfoFromStream { get { return currentStationMSSWrapper?.StationInfo; } }

        public static async Task<bool> PlayStationAsync(StationModel station)
        {
            if (station == currentStationModel && IsPlaying) return true;

            if (IsPlaying)
            {
                var pause = new ValueSet();
                pause.Add(Messages.PauseMessage, "");

                BackgroundMediaPlayer.SendMessageToBackground(pause);
            }

            currentStationModel = station;

            //TODO use a combo of events+anon-delegates and TaskCompletionSource to detect play back errors here to seperate connection errors from long-running audio errors.
            //handle error when connecting.

            MediaPlayer currentMediaPlayer = BackgroundMediaPlayer.Current;

            TaskCompletionSource<object> errorTaskSource = new TaskCompletionSource<object>();
            TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs> errorHandler = null;
            errorHandler = new TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs>((MediaPlayer sender, MediaPlayerFailedEventArgs args) =>
            {
                //TODO extend said above magic to handle messages from the background audio player
                currentMediaPlayer.MediaFailed -= errorHandler;
                errorTaskSource.TrySetResult(false);
            });
            currentMediaPlayer.MediaFailed += errorHandler;

            //handle successful connection
            TaskCompletionSource<object> successTaskSource = new TaskCompletionSource<object>();
            TypedEventHandler<MediaPlayer, object> successHandler = null;
            successHandler = new TypedEventHandler<MediaPlayer, object>((MediaPlayer sender, object args) =>
            {
                if (currentMediaPlayer.CurrentState == MediaPlayerState.Playing)
                {
                    currentMediaPlayer.CurrentStateChanged -= successHandler;
                    successTaskSource.TrySetResult(true);
                }
            });
            currentMediaPlayer.CurrentStateChanged += successHandler;


            var stream = station.Streams.First();

            var payload = new ValueSet();
            payload.Add(Messages.PlayStationMessage, JsonHelper.ToJson<PlayStationMessage>(new PlayStationMessage(stream.Url, stream.SampleRate, stream.RelativePath, station.Name, Enum.GetName(typeof(StationModelStreamServerType), stream.ServerType))));

            BackgroundMediaPlayer.SendMessageToBackground(payload);

            if (successTaskSource.Task == await Task.WhenAny(errorTaskSource.Task, successTaskSource.Task))
            {
                //successful connection
                currentMediaPlayer.MediaFailed -= errorHandler;

                if (CurrentStationChanged != null) CurrentStationChanged(null, EventArgs.Empty);

                return true;
            }
            else
            {
                //unsuccessful connection
                currentMediaPlayer.CurrentStateChanged -= successHandler;

                if (BackgroundAudioError != null) BackgroundAudioError(null, EventArgs.Empty);

                return false;
            }
        }

        public static event EventHandler<ShoutcastMediaSourceStreamMetadataChangedEventArgs> MetadataChanged;
        public static event EventHandler CurrentStationChanged;
        public static event EventHandler BackgroundAudioError;
    }
}
