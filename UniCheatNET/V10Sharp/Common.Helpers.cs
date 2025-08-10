using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace V10Sharp;

public static class Helpers
{
    /// <summary>Repeats the specified action.</summary>
    /// <param name="action">The action to repeat.</param>
    /// <param name="times">Repeat count.</param>
    public static void Repeat(Action action, int times)
    {
        while (times-- > 0)
            action.Invoke();
    }

    /// <summary>Returns an infinite enumerable of input.</summary>
    /// <typeparam name="T">Input collection type</typeparam>
    /// <param name="input">The input sequence.</param>
    /// <param name="start">The start index.</param>
    /// <returns>infinite enumerable of input</returns>
    public static IEnumerable<T> RoundRobin<T>(T[] input, int start = 0)
    {
        while (true)
        {
            for (int i = start; i < input.Length; i++)
            {
                yield return input[i];
            }
            start = 0;
        }
    }
}
