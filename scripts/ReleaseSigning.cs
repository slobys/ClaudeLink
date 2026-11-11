using System.Security.Cryptography;

// Used only by the local release scripts. Private keys never enter the project or release assets.
public static class ClaudeLinkReleaseSigning
{
    public static byte[] NewKey()
    {
        using (var key = ECDsa.Create(ECCurve.NamedCurves.nistP256)) return key.ExportPkcs8PrivateKey();
    }
    public static string PublicKey(byte[] privateKey)
    {
        using (var key = ECDsa.Create())
        {
            int read; key.ImportPkcs8PrivateKey(privateKey, out read);
            return System.Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        }
    }
    public static byte[] Sign(byte[] privateKey, byte[] payload)
    {
        using (var key = ECDsa.Create())
        {
            int read; key.ImportPkcs8PrivateKey(privateKey, out read);
            return key.SignData(payload, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
    }
}
