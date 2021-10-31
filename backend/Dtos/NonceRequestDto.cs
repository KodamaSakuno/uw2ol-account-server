using Backend.Validations;

namespace Backend.Dtos;

public class NonceRequestDto
{
    [EthereumAddress]
    public string Address { get; set; } = default!;
}
