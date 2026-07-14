namespace FWO.Basics.Extensions
{
    public static class EnumExtensions
    {
        extension<TEnum>(IEnumerable<TEnum> values) where TEnum : struct, Enum
        {
            /// <summary>
            /// Returns the sequence without the excluded enum values.
            /// </summary>
            public IEnumerable<TEnum> Except(params TEnum[] excludedValues)
            {
                ArgumentNullException.ThrowIfNull(values);
                ArgumentNullException.ThrowIfNull(excludedValues);

                return excludedValues.Length switch
                {
                    0 => values,
                    1 => ExceptSingle(values, excludedValues[0]),
                    2 => ExceptPair(values, excludedValues[0], excludedValues[1]),
                    _ => ExceptMany(values, excludedValues),
                };
            }
        }

        private static IEnumerable<TEnum> ExceptSingle<TEnum>(IEnumerable<TEnum> values, TEnum excludedValue)
            where TEnum : struct, Enum
        {
            foreach (TEnum value in values)
            {
                if (!EqualityComparer<TEnum>.Default.Equals(value, excludedValue))
                {
                    yield return value;
                }
            }
        }

        private static IEnumerable<TEnum> ExceptPair<TEnum>(IEnumerable<TEnum> values, TEnum firstExcludedValue, TEnum secondExcludedValue)
            where TEnum : struct, Enum
        {
            foreach (TEnum value in values)
            {
                if (!EqualityComparer<TEnum>.Default.Equals(value, firstExcludedValue)
                    && !EqualityComparer<TEnum>.Default.Equals(value, secondExcludedValue))
                {
                    yield return value;
                }
            }
        }

        private static IEnumerable<TEnum> ExceptMany<TEnum>(IEnumerable<TEnum> values, TEnum[] excludedValues)
            where TEnum : struct, Enum
        {
            HashSet<TEnum> excluded = [.. excludedValues];

            foreach (TEnum value in values)
            {
                if (!excluded.Contains(value))
                {
                    yield return value;
                }
            }
        }
    }
}
