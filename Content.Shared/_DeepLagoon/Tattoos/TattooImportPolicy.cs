namespace Content.Shared._DeepLagoon.Tattoos;

public static class TattooImportPolicy
{
    public static bool CanImportExternalPng(int supporterTier) => supporterTier >= 3;
}
