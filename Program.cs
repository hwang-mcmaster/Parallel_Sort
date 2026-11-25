using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

// One name = one small value type. No separate Models/ folder needed.
readonly record struct Person(string First, string Last)
{
    public override string ToString() => $"{First} {Last}";
}

class Program
{

    static readonly StringComparer NameOrder = StringComparer.Ordinal;

    static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "names.txt";

        // 1. Read names from file 
        Person[] names;
        try
        {
            names = ReadNames(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        Console.WriteLine("Parallel Alphabetical Sort of Names");
        Console.WriteLine($"Records loaded: {names.Length:N0}");


        return 0;
    }
    // File handling

    static Person[] ReadNames(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Input file not found: '{Path.GetFullPath(path)}'");

        const int MaxErrorsShown = 10;
        var names = new List<Person>();
        var errors = new List<string>();
        int badLines = 0;
        int lineNo = 0;

        foreach (string line in File.ReadLines(path))
        {
            lineNo++;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            int space = line.IndexOf(' ');
            if (space >= 0 && line.IndexOf(' ', space + 1) < 0)
            {
                names.Add(new Person(line[..space], line[(space + 1)..]));
            }
            else
            {
                badLines++;
                if (errors.Count < MaxErrorsShown)
                    errors.Add($"  line {lineNo}: \"{line}\" (expected exactly one space)");
            }
        }

        if (badLines > 0)
        {
            string more = badLines > MaxErrorsShown ? $"{Environment.NewLine}  ...and {badLines - MaxErrorsShown} more" : "";
            throw new InvalidDataException(
                $"{badLines} malformed line(s) in '{path}' (expected 'FirstName LastName'):" +
                Environment.NewLine + string.Join(Environment.NewLine, errors) + more);
        }

        return names.ToArray();
    }

    // Comparison
    // Last name first; first name breaks ties. Returns <0, 0, or >0 like
    // string.Compare. Returns 0 only for identical full names
    static int CompareNames(Person a, Person b)
    {
        int byLast = NameOrder.Compare(a.Last, b.Last);
        return byLast != 0 ? byLast : NameOrder.Compare(a.First, b.First);
    }

    static void ParallelMergeSort(Person[] a, Person[] buffer, int lo, int hi, int depth)
    {
        const int MinParallelSize = 8192;
        int maxDepth = (int)Math.Log2(Environment.ProcessorCount);

        // Base case: zero or one element is already sorted
        if (hi - lo <= 1)
            return;

        // Use sequential sorting for smaller partitions
        // or when we've reached the parallel depth limit
        if (hi - lo <= MinParallelSize || depth >= maxDepth)
        {
            SequentialMergeSort(a, buffer, lo, hi);
            return;
        }

        // Split into two halves
        int mid = lo + (hi - lo) / 2;

        // Sort both halves concurrently
        Parallel.Invoke(
            () => ParallelMergeSort(a, buffer, lo, mid, depth + 1),
            () => ParallelMergeSort(a, buffer, mid, hi, depth + 1)
        );

        // Merge once both halves finish
        Merge(a, buffer, lo, mid, hi);
    }

    static void SequentialMergeSort(Person[] a, Person[] buffer, int lo, int hi)
    {
        // Base case: zero or one element
        if (hi - lo <= 1)
            return;

        // Divide into two halves
        int mid = lo + (hi - lo) / 2;

        // Sort each half sequentially
        SequentialMergeSort(a, buffer, lo, mid);
        SequentialMergeSort(a, buffer, mid, hi);

        // Merge sorted halves
        Merge(a, buffer, lo, mid, hi);
    }

    static void Merge(Person[] a, Person[] buffer, int lo, int mid, int hi)
    {
        int i = lo;   // Left half index
        int j = mid;  // Right half index
        int k = lo;   // Buffer index

        // Merge both sorted halves
        while (i < mid && j < hi)
        {
            if (CompareNames(a[i], a[j]) <= 0)
                buffer[k++] = a[i++];
            else
                buffer[k++] = a[j++];
        }

        // Copy remaining elements from left half
        while (i < mid)
            buffer[k++] = a[i++];

        // Copy remaining elements from right half
        while (j < hi)
            buffer[k++] = a[j++];

        // Copy merged results back into original array
        Array.Copy(buffer, lo, a, lo, hi - lo);
    }
}
