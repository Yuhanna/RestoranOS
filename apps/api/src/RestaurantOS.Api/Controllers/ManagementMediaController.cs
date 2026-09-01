using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Infrastructure;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/media")]
public sealed class ManagementMediaController(IStockPhotoLibrary stockPhotos) : ControllerBase
{
    [HttpGet("stock")]
    [Authorize(Policy = ManagementPolicies.MenuView)]
    [ProducesResponseType(typeof(ManagementStockPhotoLibraryResponse), StatusCodes.Status200OK)]
    public IActionResult ListStockPhotos([FromQuery] string? query, [FromQuery] string? category)
    {
        var result = stockPhotos.List(query, category);
        return Ok(new ManagementStockPhotoLibraryResponse(
            result.Categories
                .Select(categoryItem => new ManagementStockPhotoCategoryResponse(categoryItem.Id, categoryItem.Label))
                .ToArray(),
            result.Photos
                .Select(photo => new ManagementStockPhotoResponse(
                    photo.Id,
                    photo.Category,
                    photo.Path,
                    photo.Title,
                    photo.Alt,
                    photo.Tags))
                .ToArray()));
    }
}
