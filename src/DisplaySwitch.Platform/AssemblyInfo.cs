using System.Runtime.CompilerServices;

// All interop goes through LibraryImport source generation with blittable structs, so the legacy
// runtime marshaller is never needed. Disabling it lets InlineArray buffers cross the boundary directly.
[assembly: DisableRuntimeMarshalling]
