using NUnit.Framework;

// Controls the maximum number of worker threads used by NUnit
// Setting to 1 ensures all tests run sequentially
[assembly: LevelOfParallelism(1)]

// Optional: Disable parallelization at assembly level
[assembly: Parallelizable(ParallelScope.None)]
