using DiscordBotApi.Data.AudioPanels;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace DiscordBotApi.Utilities;

public class AudioPanelUtils
{
    public static async Task CreateAudioPanel(ApplicationCommandModule<ApplicationCommandContext> module,
        ApplicationDbContext dbContext, ulong guildId, ulong channelId)
    {
        var audioTracks = dbContext.AudioTracks
            .AsNoTracking()
            .Include(t => t.Guild)
            .Where(t => t.Guild.Id == guildId)
            .ToList();

        if (audioTracks == null || audioTracks.Count == 0)
        {
            await module.ModifyResponseAsync(m => m.Content = "Отсутствуют данные в базе данных");
            return;
        }

        AudioPanel? audioPanel = dbContext.AudioPanels
            .FirstOrDefault(ap => ap.GuildId == guildId);

        if (audioPanel == null)
        {
            audioPanel = new();
            dbContext.AudioPanels.Add(audioPanel);
        }
        else
        {
            //remove prev panel
            await TryDeleteAudioPanelMessages(audioPanel, module.Context);
        }

        var buttons = new List<ButtonProperties>
        {
            new($"{VoiceConnectionConstants.VoicePanelStopButtonId}", "ОСТАНОВИТЬ", ButtonStyle.Danger)
        };

        foreach (var track in audioTracks)
        {
            buttons.Add(new ButtonProperties(
                $"{VoiceConnectionConstants.VoicePanelButtonId}:{track.Title}", track.Title, ButtonStyle.Primary));
        }

        var actionRows = new List<ActionRowProperties>();
        for (int i = 0; i < buttons.Count; i += 5)
        {
            actionRows.Add(new(buttons.Skip(i).Take(5).ToArray()));
        }

        var panelMessagesList = new List<MessageProperties>();
        for (int i = 0; i < actionRows.Count; i += 5)
        {
            panelMessagesList.Add(new MessageProperties
            {
                Components = actionRows.Skip(i).Take(5).ToArray()
            });
        }

        MessageProperties[] panelMessages = panelMessagesList.ToArray();

        if (panelMessages.Length == 0)
        {
            await module.ModifyResponseAsync(m => m.Content = "Ошибка создания панели");
            return;
        }

        ulong[] msgIds = new ulong[panelMessages.Length];
        for (int k = 0; k < panelMessages.Length; k++)
        {
            var msg = await module.Context.Client.Rest.SendMessageAsync(channelId, panelMessages[k]);
            msgIds[k] = msg.Id;
        }

        if (module.Context.Guild != null)
            audioPanel.GuildId = module.Context.Guild.Id;

        audioPanel.ChannelId = module.Context.Channel.Id;
        audioPanel.MessageIds = msgIds;

        try
        {
            dbContext.SaveChanges();
        }
        catch { }

        await module.ModifyResponseAsync(m => m.Content = "Готово");
    }

    public static async Task TryDeleteAudioPanelMessages(AudioPanel audioPanel, ApplicationCommandContext context)
    {
        if (audioPanel.MessageIds == null || audioPanel.MessageIds.Length <= 0)
        {
            return;
        }

        foreach (var msgId in audioPanel.MessageIds)
        {
            try
            {
                await context.Client.Rest.DeleteMessageAsync(audioPanel.ChannelId, msgId);
            }
            catch { }
        }
    }
}
