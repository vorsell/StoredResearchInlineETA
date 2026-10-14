using System;

namespace ResearchBenchETACompactFix
{
    internal static class ResearchEtaTextLayout
    {
        internal static string Append(
            string progressLine,
            string etaText,
            float availableWidth,
            Func<string, float> measureWidth)
        {
            if (string.IsNullOrEmpty(etaText))
            {
                return progressLine;
            }

            string inline = progressLine + "  " + etaText;
            float width = measureWidth(inline);
            if (!float.IsNaN(width) && !float.IsInfinity(width) && width >= 0f &&
                !float.IsNaN(availableWidth) && !float.IsInfinity(availableWidth) &&
                availableWidth > 0f && width <= availableWidth)
            {
                return inline;
            }

            return progressLine + "\n" + etaText;
        }
    }
}
