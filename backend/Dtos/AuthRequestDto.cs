namespace Backend.Dtos;

public class AuthRequestDto
{
    public string Address { get; set; } = default!;
    public string Signature { get; set; } = default!;
    public string Session { get; set; } = default!;
}
