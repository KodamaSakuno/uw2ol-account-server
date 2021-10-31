using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Nethereum.Signer;
using Nethereum.Util;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Backend.Controllers;

[ApiController]
[Route("account")]
public class AccountController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly AccountService _accountService;

    public AccountController(IConfiguration configuration, AccountService accountService)
    {
        _configuration = configuration;
        _accountService = accountService;
    }

    [HttpPost("auth")]
    public async Task<IActionResult> Auth(AuthRequestDto request)
    {
        var nonce = await _accountService.GetNonceAsync(request.Address);

        if (string.IsNullOrEmpty(nonce))
            return Unauthorized(new MessageResponseDto("Signature verification failed"));

        var address = new EthereumMessageSigner().EncodeUTF8AndEcRecover($"I'm signing my one-time nonce: {nonce}", request.Signature);

        if (!AddressUtil.Current.AreAddressesTheSame(address, request.Address))
            return Unauthorized(new MessageResponseDto("Signature verification failed"));

        await _accountService.RefreshNonceAsync(address);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: new Claim[]
            {
                new("Address", address),
                new Claim("Session", request.Session),
            },
            expires: DateTime.Now.AddMinutes(5),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])), SecurityAlgorithms.HmacSha256Signature));

        return Ok(new AuthResponseDto(new JwtSecurityTokenHandler().WriteToken(token), _accountService.GetName(address)));
    }

    [HttpPost("nonce")]
    public async Task<NonceResponseDto> PrepareNonce(NonceRequestDto request) =>
        new(await _accountService.RefreshNonceAsync(request.Address));

    [HttpPost("login")]
    [Authorize]
    public IActionResult Login()
    {
        var address = User.FindFirstValue("Address");
        var session = User.FindFirstValue("Session");

        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(session))
            return Unauthorized(new MessageResponseDto("Invalid token"));

        var name = _accountService.GetName(address);

        if (string.IsNullOrEmpty(name))
            return Unauthorized(new MessageResponseDto("Require register"));

        _accountService.NotifyLogin(session, address, name);

        return Ok();
    }

    [HttpPost("register")]
    [Authorize]
    public IActionResult Register(RegisterRequestDto request)
    {
        var address = User.FindFirstValue("Address");
        var session = User.FindFirstValue("Session");

        if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(session))
            return Unauthorized(new MessageResponseDto("Invalid token"));

        _accountService.Register(address, request.Name);

        _accountService.NotifyLogin(session, address, request.Name);

        return Ok();
    }

}
