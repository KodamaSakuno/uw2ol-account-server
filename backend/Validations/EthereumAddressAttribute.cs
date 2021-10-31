using Nethereum.Util;
using System.ComponentModel.DataAnnotations;

namespace Backend.Validations;

public class EthereumAddressAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string address && AddressUtil.Current.IsValidEthereumAddressHexFormat(address);
}
