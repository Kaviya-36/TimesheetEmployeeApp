using TimeSheetAppWeb.Models.DTOs;

namespace TimeSheetAppWeb.Interfaces
{
    public interface ITokenService
    {
        public string CreateToken(TokenPayloadDto payloadDto);
    }
}
