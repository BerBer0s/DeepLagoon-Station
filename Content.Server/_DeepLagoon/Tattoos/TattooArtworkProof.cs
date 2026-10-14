using System.Security.Cryptography;
using System.Text;
using Content.Shared._DeepLagoon.Tattoos;

namespace Content.Server._DeepLagoon.Tattoos;

public sealed class TattooArtworkProof(byte[] key)
{
    private byte[] Message(Guid user, Tattoo tattoo, string nonce) => Encoding.UTF8.GetBytes(
        "deeplagoon-tattoo-v1\n" + user.ToString("N") + "\n" + nonce + "\n" + tattoo.NormalBitmap + "\n" + tattoo.GlowBitmap);

    public string Sign(Guid user, Tattoo tattoo)
    {
        var nonce = Guid.NewGuid().ToString("N");
        return nonce + ":" + Convert.ToHexString(HMACSHA256.HashData(key, Message(user, tattoo, nonce)));
    }

    public bool Verify(Guid user, Tattoo tattoo)
    {
        var proof = tattoo.ArtworkProof;
        if (proof == null || proof.Length != 97 || proof[32] != ':' || !Guid.TryParseExact(proof[..32], "N", out _)) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(proof[33..]),
                HMACSHA256.HashData(key, Message(user, tattoo, proof[..32])));
        }
        catch (FormatException) { return false; }
    }
}
