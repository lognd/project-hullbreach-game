using System.Runtime.CompilerServices;

// Array-parameter FEM kernels are internal (Sonar S2368); the test
// assembly reaches them here (see docs/reference/hullbreach-structure.md).
[assembly: InternalsVisibleTo("Hullbreach.Structure.Tests")]
// The engine-free tools/plaincs build compiles the same sources under this name.
[assembly: InternalsVisibleTo("Hullbreach.Plain.Tests")]
