using DiscordBotApi.Data.AudioPanels;
using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Data.Settings;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Services;
using DiscordBotApi.Utilities;
using DiscordBotApi.Utilities.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Rest;
using NetCord.Services;
using Serilog;
using System.Diagnostics;
using System.Security.Claims;

namespace DiscordBotApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin, Moderator")]
[Route("audio-tracks")]
public class AudioTracksController(
    ApplicationDbContext context,
    RestClient restClient,
    VoiceInstancesContainer voiceInstancesContainer,
    IServiceScopeFactory serviceScopeFactory,
    IHostApplicationLifetime hostApplicationLifetime) : ControllerBase
{
    readonly ApplicationDbContext _dbContext = context;
    readonly RestClient _restClient = restClient;
    readonly VoiceInstancesContainer _voiceInstancesContainer = voiceInstancesContainer;
    readonly IServiceScopeFactory _scopeFactory = serviceScopeFactory;
    readonly IHostApplicationLifetime _applicationLifetime = hostApplicationLifetime;
    readonly static string _ffmpegPath = Environment.GetEnvironmentVariable("FFMPEG_FILE_PATH") ?? "ffmpeg";

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UploadAudioTrack([FromForm] AudioTrackUploadDto audioTrackDto)
    {
        if (audioTrackDto == null
            || string.IsNullOrEmpty(audioTrackDto.Title)
            || string.IsNullOrEmpty(audioTrackDto.GuildId)
            || audioTrackDto.File == null
            || !ulong.TryParse(audioTrackDto.GuildId, out var guildId))
        {
            return BadRequest("Provided data is not valid");
        }

        var result = await AudioFilesService.TrySaveFile(_dbContext, audioTrackDto.File, audioTrackDto.Title, guildId);
        return result switch
        {
            BadRequesError bre => BadRequest(bre.Message),
            AlreadyExistError => BadRequest(new ProblemDetails()
                                    {
                                        Status = StatusCodes.Status409Conflict,
                                        Detail = "Already exist"
                                    }),
            Success<AudioTrack> => Created(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpGet("get")]
    [ProducesResponseType<AudioTrackDto>(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAudioTrackByTitle([FromBody] AudioTrackGetByTitleDto audioTrackDto)
    {
        if (string.IsNullOrEmpty(audioTrackDto.Title))
            return BadRequest("Provided data is not valid");

        var track = _dbContext.AudioTracks
            .AsNoTracking()
            .Where(t => t.Guild.Id == audioTrackDto.GuildId && t.Title == audioTrackDto.Title)
            .Select(t => new AudioTrackDto()
            {
                Key = t.Key,
                Title = t.Title,
                CreatedAt = t.CreatedAt.ToString(),
                SizeInKb = t.SizeInKb,
                GuildId = t.Guild.Id.ToString(),
                DownloadUrl = $"/audio-tracks/{t.Key}/download",
            })
            .FirstOrDefault();

        if (track == null)
            return NotFound();

        return Ok(track);
    }

    [HttpGet("get-all")]
    [ProducesResponseType<IEnumerable<AudioTrackDto>>(200)]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAudioTracks([FromQuery] ulong guildId)
    {
        var tracks = _dbContext.AudioTracks
            .AsNoTracking()
            .Where(t => t.Guild.Id == guildId)
            .Select(t => new AudioTrackDto()
            {
                Key = t.Key,
                Title = t.Title,
                CreatedAt = t.CreatedAt.ToString(),
                SizeInKb = t.SizeInKb,
                GuildId = t.Guild.Id.ToString(),
                DownloadUrl = $"/audio-tracks/{t.Key}/download",
            })
            .AsEnumerable();

        if (tracks == null)
            return NotFound();

        return Ok(tracks);
    }

    [HttpGet("{key:int}/download")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadAudioTrack(int key)
    {
        var track = _dbContext.AudioTracks
            .AsNoTracking()
            .Where(t => t.Key == key)
            .Select(t => new AudioTrackGetByTitleDto()
            {
                GuildId = t.Guild.Id,
                Title = t.Title,
            })
            .FirstOrDefault();

        if (track == null)
            return NotFound();

        var filePath = AudioFilesService.GetAudioTrackPath(track.GuildId, track.Title!);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var stream = System.IO.File.OpenRead(filePath);
        return File(stream, "audio/mpeg", fileDownloadName: $"{track.Title}.mp3", enableRangeProcessing: true);
    }


    [HttpPost("share")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> ShareAudioTrack([FromBody] AudioTrackShareDto audioTrackDto)
    {
        if (string.IsNullOrWhiteSpace(audioTrackDto.GuildId) || !ulong.TryParse(audioTrackDto.GuildId, out var guildId))
            return BadRequest("Guild id is incorrect");

        var track = _dbContext.AudioTracks
            .AsNoTracking()
            .FirstOrDefault(t => t.Key == audioTrackDto.Key);

        if (track == null)
            return NotFound("Audio track not found");

        var filePath = AudioFilesService.GetAudioTrackPath(guildId, track.Title);

        if (string.IsNullOrWhiteSpace(filePath)
            || !System.IO.File.Exists(filePath))
            return StatusCode(StatusCodes.Status500InternalServerError, "File path corrupted");

        var guild = _dbContext.Guilds
            .FirstOrDefault(g => g.Id == guildId);

        if (guild == null)
            return NotFound("Guild not found");

        using var sourceFileStream = System.IO.File.OpenRead(filePath);

        var result = await AudioFilesService.TrySaveFile(_dbContext, sourceFileStream, track.SizeInKb ?? 0, track.Title, guildId);
        return result switch
        {
            BadRequesError bre => BadRequest(bre.Message),
            AlreadyExistError => BadRequest(new ProblemDetails()
            {
                Status = StatusCodes.Status409Conflict,
                Detail = "Already exist"
            }),
            Success<AudioTrack> => Created(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPatch("rename")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RenameAudioTrack(AudioTrackRenameDto audioTrackDto)
    {
        if (string.IsNullOrWhiteSpace(audioTrackDto.NewTitle)
            || !AudioFilesService.IsTitleCorrect(audioTrackDto.NewTitle))
            return BadRequest("Incorrect new title");

        var track = _dbContext.AudioTracks
            .Include(t => t.Guild)
            .FirstOrDefault(t => t.Key == audioTrackDto.Key);

        if (track == null)
            return NotFound();

        var filePath = AudioFilesService.GetAudioTrackPath(track.Guild.Id, track.Title);

        if (string.IsNullOrWhiteSpace(filePath)
            || !System.IO.File.Exists(filePath))
            return StatusCode(StatusCodes.Status500InternalServerError, "Track's path is empty");

        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
                return StatusCode(StatusCodes.Status500InternalServerError, "Track's path is corrupted");

            string newFullPath = Path.Combine(directory, $"{audioTrackDto.NewTitle}.mp3");

            if (System.IO.File.Exists(newFullPath))
                return Conflict("File with this title already exist");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (System.IO.File.Exists(filePath))
                System.IO.File.Move(filePath, newFullPath);
            else
                return StatusCode(StatusCodes.Status500InternalServerError, "Track's path is corrupted");

            track.Title = audioTrackDto.NewTitle;
            _dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception on renaming audio file with key: {0}", track.Key);
            return StatusCode(StatusCodes.Status500InternalServerError, "Error on renaming file");
        }

        return Ok();
    }

    [HttpPost("delete")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteAudioTrack([FromBody] AudioTrackDeleteDto audioTrackDto)
    {
        if (string.IsNullOrEmpty(audioTrackDto.Title))
            return BadRequest("Provided data is not valid");

        var res = AudioFilesService.DeleteFile(_dbContext, audioTrackDto.GuildId, audioTrackDto.Title);

        return res switch
        {
            BadRequesError => BadRequest(res.Message),
            NotFoundError => NotFound(res.Message),
            InternalError => StatusCode(StatusCodes.Status500InternalServerError),
            _ => Ok()
        };
    }

    [HttpPost("{key:int}/play")]
    [ProducesResponseType(202)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> PlayAudioTrack(int key, [FromQuery] ulong guildId)
    {
        if (key < 0 || guildId <= 0)
            return BadRequest("Invalid data");

        var track = _dbContext.AudioTracks
           .AsNoTracking()
           .Where(t => t.Guild.Id == guildId && t.Key == key)
           .FirstOrDefault();

        if (track == null)
            return NotFound();

        var filePath = AudioFilesService.GetAudioTrackPath(guildId, track.Title);
        if (string.IsNullOrWhiteSpace(filePath)
            || !System.IO.File.Exists(filePath))
            return StatusCode(StatusCodes.Status500InternalServerError, "Track path is corrupted");

        if (!_voiceInstancesContainer.VoiceInstances.ContainsKey(guildId))
            return BadRequest("Bot is not in voice channel");

        var shutdownToken = _applicationLifetime.ApplicationStopping;

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            var voiceContainer = sp.GetRequiredService<VoiceInstancesContainer>();

            try
            {
                if (!voiceContainer.VoiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
                {
                    Log.Error("User with id: {0} invoke audio play, but bot was not connected", User.FindFirstValue(ClaimTypes.NameIdentifier));
                    return;
                }

                // Take a playback job
                using var job = voiceInstance.TryEnterJob(VoiceJobType.Playing);
                if (job is not { CancellationToken: var jobToken })
                    return;

                // Link job cancellation with app shutdown
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    shutdownToken, jobToken);

                var voiceClient = voiceInstance.Client;
                await voiceClient.EnterSpeakingStateAsync(new(SpeakingFlags.Microphone));

                using var voiceStream = voiceClient.CreateVoiceStream();
                using OpusEncodeStream opusEncodeStream = new(voiceStream,
                                                              PcmFormat.Float,
                                                              VoiceChannels.Stereo,
                                                              OpusApplication.Audio);

                using var ffmpeg = Process.Start(new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    ArgumentList =
                {
                    "-i", filePath,
                    "-f", BitConverter.IsLittleEndian ? "f32le" : "f32be",
                    "-ar", "48000",
                    "-ac", "2",
                    "pipe:1",
                },
                    RedirectStandardOutput = true,
                })!;

                var ffmpegOutput = ffmpeg.StandardOutput.BaseStream;
                var token = linked.Token;

                try
                {
                    await ffmpegOutput.CopyToAsync(opusEncodeStream, token);
                    await opusEncodeStream.FlushAsync(token);
                }
                catch (Exception ex)
                {
                    ffmpeg.Kill();

                    if (ex is not OperationCanceledException and not AggregateException { InnerException: OperationCanceledException })
                        throw;
                }
            }
            catch (OperationCanceledException) when (shutdownToken.IsCancellationRequested)
            {
                // App is shutting down
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Voice streaming failed for guild {GuildId}", guildId);
            }
        });

        return Accepted();
    }

    [HttpPost("stop")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> StopAudioTrack([FromQuery] string guildId)
    {
        if (string.IsNullOrWhiteSpace(guildId)
            || !ulong.TryParse(guildId, out var id))
            return BadRequest("Invalid data");

        if (!_voiceInstancesContainer.VoiceInstances.TryGetValue(id, out var instance) || instance is null)
            return BadRequest("Bot is not in voice channel");

        if (!instance.StopPlaying())
            return BadRequest("Nothing is playing");

        return Ok();
    }

    [HttpPost("update-audio-panel")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateAudioPanel(AudioTrackUpdatePanelDto updateDto)
    {
        if (string.IsNullOrWhiteSpace(updateDto.GuildId)
            || string.IsNullOrWhiteSpace(updateDto.ChannelId)
            || !ulong.TryParse(updateDto.GuildId, out var guildId)
            || !ulong.TryParse(updateDto.ChannelId, out var channelId))
        {
            return BadRequest("Incorrect data");
        }

        var audioTracks = _dbContext.AudioTracks
            .AsNoTracking()
            .Include(t => t.Guild)
            .Where(t => t.Guild.Id == guildId)
            .ToList();

        if (audioTracks == null || audioTracks.Count == 0)
        {
            return NotFound("Audio tracks not found");
        }

        AudioPanel? audioPanel = _dbContext.AudioPanels
            .FirstOrDefault(ap => ap.GuildId == guildId);

        if (audioPanel == null)
        {
            audioPanel = new();
            _dbContext.AudioPanels.Add(audioPanel);
        }
        else
        {
            //remove prev panel
            if (audioPanel.MessageIds != null && audioPanel.MessageIds.Length > 0)
            {
                foreach (var msgId in audioPanel.MessageIds)
                {
                    try
                    {
                        await _restClient.DeleteMessageAsync(audioPanel.ChannelId, msgId);
                    }
                    catch { }
                }
            }
        }

        var theme = _dbContext.Settings
            .AsNoTracking()
            .Where(s => s.Guild.Id == guildId)
            .Select(s => s.ButtonsColorTheme)
            .FirstOrDefault();

        var buttons = new List<ButtonProperties>
        {
            new($"{VoiceConnectionConstants.VoicePanelStopButtonId}", "ОСТАНОВИТЬ", ButtonStyle.Danger)
        };

        for (int i = 0; i < audioTracks.Count; i++)
        {
            AudioTrack? track = audioTracks[i];
            buttons.Add(new ButtonProperties(
                $"{VoiceConnectionConstants.VoicePanelButtonId}:{track.Title}", track.Title, AudioPanelUtils.GetCurrentButtonStyle(theme, i)));
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
            return StatusCode(StatusCodes.Status500InternalServerError, "Panel created with no messages");
        }

        ulong[] msgIds = new ulong[panelMessages.Length];
        for (int k = 0; k < panelMessages.Length; k++)
        {
            var msg = await _restClient.SendMessageAsync(channelId, panelMessages[k]);
            msgIds[k] = msg.Id;
        }

        audioPanel.GuildId = guildId;
        audioPanel.ChannelId = channelId;
        audioPanel.MessageIds = msgIds;

        try
        {
            _dbContext.SaveChanges();
        }
        catch 
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Error on writing to the database");
        }

        return Created();
    }

    [HttpPost("theme")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ChangeAudioPanelTheme([FromQuery] string guildId, [FromQuery] ButtonsColorTheme theme)
    {
        if (string.IsNullOrWhiteSpace(guildId)
            || !ulong.TryParse(guildId, out var id))
            return BadRequest();

        var settings = _dbContext.Settings.FirstOrDefault(s => s.Guild.Id == id);
        if (settings == null)
        {
            var guild = _dbContext.Guilds.FirstOrDefault(g => g.Id == id);
            if (guild == null)
                return NotFound("Guild not found");

            var set = new Settings()
            {
                Guild = guild,
                ButtonsColorTheme = theme
            };

            _dbContext.Settings.Add(set);
            _dbContext.SaveChanges();
            return Ok();
        }

        settings.ButtonsColorTheme = theme;
        _dbContext.SaveChanges();
        return Ok();
    }
}
