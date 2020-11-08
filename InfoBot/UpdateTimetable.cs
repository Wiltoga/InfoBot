﻿using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Infobot
{
    internal static class UpdateTimetable
    {
        #region Private Fields

        private static Timer timer;

        #endregion Private Fields

        #region Public Methods

        public static void Setup()
        {
            timer = new Timer(Settings.CurrentSettings.timetableDelay.Value.TotalMilliseconds);
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Elapsed += async (sender, e) => await Update().ConfigureAwait(false);
            _ = Update();
        }

        public static async Task Update()
        {
            string regex = Uri.EscapeDataString("/^(.*) - .* - .* - .*$/");
            await Task.WhenAll(Settings.CurrentSettings.timetableUrls.Select(async (url, index) =>
            {
                if (url.Length > 0)
                {
                    var oldHash = Settings.CurrentSettings.oldHash[index];
                    var jsonRequest = $"{Program.WildgoatApi}/ical-json.php?url={Uri.EscapeDataString(url)}&weeks=2";
                    Program.Logger.Info($"Updating {index / 2 + 1}.{1 + index % 2} timetable");
                    Program.Logger.Info($"Requesting '{jsonRequest}'");
                    string json;
                    {
                        var task = Program.Client.GetStringAsync(jsonRequest);
                        if (await Task.WhenAny(task, Task.Delay(Program.Timeout)).ConfigureAwait(false) == task && task.IsCompletedSuccessfully)
                        {
                            json = task.Result;
                            var newHash = Utilities.GetSimplifiedString(json).Length;
                            if (newHash != oldHash)
                            {
                                var channelTask = Program.Discord.GetChannelAsync(Settings.CurrentSettings.timetableChannels[index]);
                                if ((await Task.WhenAny(channelTask, Task.Delay(Program.Timeout)).ConfigureAwait(false)) == channelTask && channelTask.IsCompletedSuccessfully)
                                {
                                    var channel = channelTask.Result;
                                    var oldMessages = (await channel.GetMessagesAsync(20, null, null, channel.LastMessageId).ConfigureAwait(false)).Where(m => m.Author.IsCurrent);
                                    if (oldMessages.Any())
                                        await channel.DeleteMessagesAsync(oldMessages).ConfigureAwait(false);
                                    var error = false;
                                    {
                                        var getTask = Program.Client.GetAsync($"{Program.WildgoatApi}/ical-png.php?url={Uri.EscapeDataString(url)}&regex={regex}");
                                        if ((await Task.WhenAny(getTask, Task.Delay(Program.Timeout)).ConfigureAwait(false)) == getTask && getTask.IsCompletedSuccessfully)
                                        {
                                            var response = getTask.Result;
                                            var sendTask = channel.SendMessageAsync(embed: new DiscordEmbedBuilder().WithTitle("Emploi du temps semaine en cours")
                                            .WithImageUrl($"{Program.WildgoatApi}/{response.Headers.Location}"));
                                            if (await Task.WhenAny(sendTask, Task.Delay(Program.Timeout)).ConfigureAwait(false) != sendTask || !sendTask.IsCompletedSuccessfully)
                                            {
                                                error = true;
                                                Program.Logger.Warning($"Unable to send the week 1 for {index / 2 + 1}.{1 + index % 2}");
                                            }
                                        }
                                        else
                                        {
                                            error = true;
                                            Program.Logger.Warning($"Unable to get the week 1 for {index / 2 + 1}.{1 + index % 2}");
                                        }
                                    }
                                    {
                                        var getTask = Program.Client.GetAsync($"{Program.WildgoatApi}/ical-png.php?url={Uri.EscapeDataString(url)}&regex={regex}&offset=1");
                                        if ((await Task.WhenAny(getTask, Task.Delay(Program.Timeout)).ConfigureAwait(false)) == getTask && getTask.IsCompletedSuccessfully)
                                        {
                                            var response = getTask.Result;
                                            var sendTask = channel.SendMessageAsync(embed: new DiscordEmbedBuilder().WithTitle("Emploi du temps semaine prochaine")
                                            .WithImageUrl($"{Program.WildgoatApi}/{response.Headers.Location}"));
                                            if (await Task.WhenAny(sendTask, Task.Delay(Program.Timeout)).ConfigureAwait(false) != sendTask || !sendTask.IsCompletedSuccessfully)
                                            {
                                                error = true;
                                                Program.Logger.Warning($"Unable to send the week 2 for {index / 2 + 1}.{1 + index % 2}");
                                            }
                                        }
                                        else
                                        {
                                            error = true;
                                            Program.Logger.Warning($"Unable to get the week 2 for {index / 2 + 1}.{1 + index % 2}");
                                        }
                                    }
                                    if (!error)
                                        Settings.CurrentSettings.oldHash[index] = newHash;
                                }
                                else
                                    Program.Logger.Warning($"Unable to find {index / 2 + 1}.{1 + index % 2} timetable channel.");
                            }
                            else
                                Program.Logger.Info($"No differences detected for {index / 2 + 1}.{1 + index % 2}");
                        }
                        else
                            Program.Logger.Warning("Unable to get the JSON");
                    }
                }
                else
                    Program.Logger.Warning($"No url provided for {index / 2 + 1}.{1 + index % 2}");
            })).ConfigureAwait(false);
            Program.Logger.Info("Timetables updated");
            SettingsManager.Save(Settings.CurrentSettings);
        }

        #endregion Public Methods
    }
}