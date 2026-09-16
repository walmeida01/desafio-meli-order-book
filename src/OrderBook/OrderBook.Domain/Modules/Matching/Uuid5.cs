namespace OrderBook.Domain.Modules.Matching;

internal static class Uuid5
{
    public static Guid Create(Guid ns, string value)
    {
        using var sha = System.Security.Cryptography.SHA1.Create();
        var nsBytes = ns.ToByteArray();
        Swap(nsBytes); var bytes = System.Text.Encoding.UTF8.GetBytes(value); var input = new byte[nsBytes.Length + bytes.Length]; Buffer.BlockCopy(nsBytes, 0, input, 0, nsBytes.Length); Buffer.BlockCopy(bytes, 0, input, nsBytes.Length, bytes.Length);
        var hash = sha.ComputeHash(input); hash[6] = (byte)((hash[6] & 0x0f) | 0x50); hash[8] = (byte)((hash[8] & 0x3f) | 0x80); var result = new byte[16]; Buffer.BlockCopy(hash, 0, result, 0, 16); Swap(result); return new Guid(result);
    }
    private static void Swap(byte[] b) { (b[0], b[3]) = (b[3], b[0]); (b[1], b[2]) = (b[2], b[1]); (b[4], b[5]) = (b[5], b[4]); (b[6], b[7]) = (b[7], b[6]); }
}
