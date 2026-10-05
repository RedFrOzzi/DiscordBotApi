using DiscordBotApi.Data.AudioPanels;
using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.BotFeatures.VoiceConnection;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using Serilog;

namespace DiscordBotApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin, Moderator")]
[Route("audio-tracks")]
public class AudioTracksController(ApplicationDbContext context, RestClient restClient) : ControllerBase
{
    readonly ApplicationDbContext _dbContext = context;
    private readonly RestClient _restClient = restClient;

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
            .FirstOrDefault(t => t.Key == key);

        if (track == null || string.IsNullOrEmpty(track.Path))
            return NotFound();

        if (!System.IO.File.Exists(track.Path))
            return NotFound();

        var stream = System.IO.File.OpenRead(track.Path);
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

        if (string.IsNullOrWhiteSpace(track.Path))
            return StatusCode(StatusCodes.Status500InternalServerError, "File path corrupted");

        var guild = _dbContext.Guilds
            .FirstOrDefault(g => g.Id == guildId);

        if (guild == null)
            return NotFound("Guild not found");

        using var sourceFileStream = System.IO.File.OpenRead(track.Path);

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
            .FirstOrDefault(t => t.Key == audioTrackDto.Key);

        if (track == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(track.Path))
            return StatusCode(StatusCodes.Status500InternalServerError, "Track's path is empty");

        try
        {
            string directory = Path.GetDirectoryName(track.Path);
            if (string.IsNullOrWhiteSpace(directory))
                return StatusCode(StatusCodes.Status500InternalServerError, "Track's path is corrupted");

            string newFullPath = Path.Combine(directory, $"{audioTrackDto.NewTitle}.mp3");

            if (System.IO.File.Exists(newFullPath))
                return Conflict("File with this title already exist");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (System.IO.File.Exists(track.Path))
                System.IO.File.Move(track.Path, newFullPath);
            else
                return StatusCode(StatusCodes.Status500InternalServerError, "Track's path is corrupted");

            track.Title = audioTrackDto.NewTitle;
            track.Path = newFullPath;
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
}
