using DraftDatastore.Application.Players;
using DraftDatastore.Domain.Entities;
using DraftDatastore.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DraftDatastore.API.Storage;
namespace DraftDatastore.API.Controllers;
[ApiController, Authorize, Route("api/v1/players")]
public sealed class PlayersController(IPlayerService service, DraftDatastoreDbContext db, IPlayerImageStorage imageStorage) : ControllerBase
{
 private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png", "image/webp"];
 [HttpGet] public Task<PagedResult<PlayerResponse>> GetAll([FromQuery] PlayerSearchRequest request,CancellationToken ct)=>service.SearchAsync(request,ct);
 [HttpGet("{id:guid}")] public async Task<ActionResult<PlayerResponse>> Get(Guid id,CancellationToken ct){var p=await service.GetAsync(id,ct);return p is null?NotFound():Ok(p);}
 [HttpPost,Authorize(Roles="Admin")] public async Task<ActionResult<PlayerResponse>> Create(PlayerUpsertRequest request,CancellationToken ct){var p=await service.CreateAsync(request,ct);return CreatedAtAction(nameof(Get),new{id=p.Id},p);}
 [HttpPut("{id:guid}"),Authorize(Roles="Admin")] public async Task<ActionResult<PlayerResponse>> Update(Guid id,PlayerUpsertRequest request,CancellationToken ct){var p=await service.UpdateAsync(id,request,ct);return p is null?NotFound():Ok(p);}
 [HttpPatch("{id:guid}"),Authorize(Roles="Admin")] public async Task<ActionResult<PlayerResponse>> Patch(Guid id,PlayerPatchRequest request,CancellationToken ct){var p=await service.PatchAsync(id,request,ct);return p is null?NotFound():Ok(p);}
 [HttpDelete("{id:guid}"),Authorize(Roles="Admin")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct)=>await service.DeleteAsync(id,ct)?NoContent():NotFound();
 [HttpPost("{id:guid}/restore"),Authorize(Roles="Admin")] public async Task<IActionResult> Restore(Guid id,CancellationToken ct)=>await service.RestoreAsync(id,ct)?NoContent():NotFound();
 [HttpPost("{id:guid}/images"),Authorize(Roles="Admin")] public async Task<ActionResult<PlayerImageUploadResponse>> Upload(Guid id,[FromForm] IFormFile file,CancellationToken ct){if(file.Length==0||file.Length>5*1024*1024)return BadRequest("Image must be under 5 MB.");if(!AllowedImageContentTypes.Contains(file.ContentType))return BadRequest("Only JPEG, PNG, and WebP are allowed.");if(!await db.Players.AnyAsync(x=>x.Id==id,ct))return NotFound();var path=$"uploads/{id:N}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";await using var stream=file.OpenReadStream();await imageStorage.SaveAsync(path,stream,file.ContentType,ct);foreach(var image in await db.PlayerImages.Where(x=>x.PlayerId==id).ToListAsync(ct))image.IsPrimary=false;db.PlayerImages.Add(new PlayerImage{PlayerId=id,BlobPath=path,ContentType=file.ContentType,IsPrimary=true});await db.SaveChangesAsync(ct);return Ok(new PlayerImageUploadResponse($"/player-images/{path}"));}
}
public sealed record PlayerImageUploadResponse(string ImageUrl);
