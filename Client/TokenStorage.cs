#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    interface ITokenStorage
    {
        string? Token { get; set; }
        string? RefreshToken { get; set; }
    }
    public class TokenStorage : ITokenStorage
    {
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
    }
}
