using System.Runtime.CompilerServices;

// Array-parameter FEM kernels are internal (Sonar S2368); the test
// assembly reaches them here (see docs/reference/hullbreach-structure.md).
[assembly: InternalsVisibleTo("Hullbreach.Structure.Tests")]
