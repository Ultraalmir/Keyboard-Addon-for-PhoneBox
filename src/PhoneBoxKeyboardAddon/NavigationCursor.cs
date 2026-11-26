namespace PhoneBoxKeyboardAddon;

/// <summary>Pure page-cursor arithmetic for the PhoneBox previous/next shortcuts.</summary>
internal static class NavigationCursor
{
    internal static int Normalize(int currentPage, int configuredStartPage, int pageCount)
    {
        if (pageCount < 1) throw new ArgumentOutOfRangeException(nameof(pageCount));
        return currentPage >= 1 && currentPage <= pageCount
            ? currentPage
            : Math.Clamp(configuredStartPage, 1, pageCount);
    }

    internal static int Move(int currentPage, int pageCount, int direction)
    {
        if (pageCount < 1) throw new ArgumentOutOfRangeException(nameof(pageCount));
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        var current = Math.Clamp(currentPage, 1, pageCount);
        return ((current - 1 + direction + pageCount) % pageCount) + 1;
    }
}
