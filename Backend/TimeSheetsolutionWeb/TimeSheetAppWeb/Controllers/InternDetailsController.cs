using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeSheetAppWeb.Interface;
using TimeSheetAppWeb.Model.DTOs;

namespace TimeSheetAppWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require authentication
    public class InternDetailsController : ControllerBase
    {
        private readonly IInternDetailsService _internDetailsService;

        public InternDetailsController(IInternDetailsService internDetailsService)
        {
            _internDetailsService = internDetailsService;
        }

        // ---------------- GET ALL ----------------
        [HttpGet]
        [Authorize(Roles = "Admin,HR,Manager,Mentor")]
        [HttpGet]
        public async Task<IActionResult> GetAll(
        int pageNumber = 1,
        int pageSize = 10,
        string? userName = null,
        string? mentorName = null)
        {
            var result = await _internDetailsService.GetAllAsync(pageNumber, pageSize, userName, mentorName);
            return Ok(result);
        }
        // ---------------- GET BY ID ----------------
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,HR,Manager,Mentor")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _internDetailsService.GetByIdAsync(id);
            return Ok(result);
        }

        // ---------------- CREATE ----------------
        [HttpPost]
        [Authorize(Roles = "Admin,HR,Manager,Mentor")]
        public async Task<IActionResult> Create([FromBody] InternDetailsCreateDto dto)
        {
            var result = await _internDetailsService.CreateAsync(dto);
            return Ok(result);
        }

        // ---------------- UPDATE ----------------
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,HR,Manager,Mentor")]
        public async Task<IActionResult> Update(int id, [FromBody] InternDetailsCreateDto dto)
        {
            var result = await _internDetailsService.UpdateAsync(id, dto);
            return Ok(result);
        }

        // ---------------- DELETE ----------------
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,HR,Manager,Mentor")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _internDetailsService.DeleteAsync(id);
            return Ok(result);
        }
    }
}