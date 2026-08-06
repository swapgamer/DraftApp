using DraftDatastore.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace DraftDatastore.API.Controllers;
[ApiController, Authorize, Route("api/v1/lookups")]
public sealed class LookupsController(DraftDatastoreDbContext db) : ControllerBase
{
 private static readonly string[] PositionCodes = ["GK", "RB", "LB", "CBST", "CBSW", "DM", "CMDLP", "CMB2B", "CAM", "RW", "LW", "SS", "ST"];
 private static readonly EraBucket[] EraBuckets = [new(1901, 1920, "1901-1920"), new(1921, 1940, "1921-1940"), new(1941, 1960, "1941-1960"), new(1961, 1980, "1961-1980"), new(1981, 1990, "1981-1990"), new(1991, 2000, "1991-2000"), new(2001, 2010, "2001-2010"), new(2011, 2020, "2011-2020"), new(2021, 9999, "2021-Present")];
 [HttpGet("positions")] public async Task<object> Positions(CancellationToken ct)=>await db.Positions.AsNoTracking().Where(x=>PositionCodes.Contains(x.Code)).OrderBy(x=>x.Code=="GK"?1:x.Code=="RB"?2:x.Code=="LB"?3:x.Code=="CBST"?4:x.Code=="CBSW"?5:x.Code=="DM"?6:x.Code=="CMDLP"?7:x.Code=="CMB2B"?8:x.Code=="CAM"?9:x.Code=="RW"?10:x.Code=="LW"?11:x.Code=="SS"?12:13).Select(x=>new{x.Id,x.Name,x.Code}).ToArrayAsync(ct);
 [HttpGet("nationalities")] public async Task<object> Nationalities(CancellationToken ct)=>await db.Nationalities.AsNoTracking().OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.IsoCode}).ToArrayAsync(ct);
 [HttpGet("eras")] public async Task<object> Eras(CancellationToken ct)=>await db.PlayingEras.AsNoTracking().OrderBy(x=>x.StartYear).Select(x=>new{x.Id,x.Name,x.StartYear,x.EndYear}).ToArrayAsync(ct);
 [HttpGet("era-buckets")] public ActionResult<object> EraBucketsLookup()=>Ok(EraBuckets.Select(x=>new { Id=x.StartYear, x.Name, x.StartYear, x.EndYear }));
}
public sealed record EraBucket(int StartYear, int EndYear, string Name);
