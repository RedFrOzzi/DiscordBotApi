using DiscordBotApi.Data.AudioTracks;
using DiscordBotApi.Database;
using DiscordBotApi.DiscordBot.Services;
using DiscordBotApi.Utilities.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiscordBotApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin, Moderator")]
[Route("audio-tracks")]
public class AudioTracksController(ApplicationDbContext context) : ControllerBase
{
    readonly ApplicationDbContext _dbContext = context;

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
                Title = t.Title,
                CreatedAt = t.CreatedAt.ToString(),
                SizeInKb = t.SizeInKb,
                GuildId = t.Guild.Id.ToString(),
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
                Title = t.Title,
                CreatedAt = t.CreatedAt.ToString(),
                SizeInKb = t.SizeInKb,
                GuildId = t.Guild.Id.ToString(),
            })
            .AsEnumerable();

        if (tracks == null)
            return NotFound();

        return Ok(tracks);
    }

    [HttpDelete("delete")]
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
}
