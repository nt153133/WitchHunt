# WitchHunt

A byte pattern matching library which uses the GreyMagic style of patterns.

## Pattern Format

Expects pattern to be in the following format:

{Pattern} {Commands} (Wild card bytes can be single ? or ?? or half bytes 4?) 

*Don't use half bytes, they just make bad patterns but it can match them*

    48 8D 05 ?? ?? ?? ?? 48 C7 43 ?? ?? ?? ?? ?? 48 8D 4B ?? 48 89 03 66 C7 43 ?? ?? ?? Add 3 TraceRelative

### Available Commands:

**Add #** - Shifts the searcher this # is from the start of the pattern. So add 1 moves us to byte 2. add 2 moves to byte 3 etc.

**Sub #** - Shifts the searcher this # is from the start of the pattern. so sub 1 moves us to byte -1. sub 2 moves us to byte -2 etc.

**Read8** - Reads a byte from the resulting address

**Read16** - Reads 2 bytes (16bits) from the resulting address

**Read32** - Reads 4 bytes (32bits) from the resulting address

**Read64** - Reads 8 bytes (64bits) from the resulting address

**TraceRelative** - Follow the relative address used in calls and lea's

## Main Functions

### IntPtr Search(string pattern)

Searches for the first match of the given pattern from the start of the data

### IntPtr Search(string pattern, IntPtr start, int maxSearchLength)

Searches for the pattern with a given starting point in the data and a max length of bytes to search.
Useful for searching for a pattern within a certain function, passing the function address as start.

### IntPtr[] SearchMany(string pattern)

Returns all the matches of a given pattern across all the data bytes.

## Examples

#### From an executable
To run the pattern search on an exe (like ffxiv_dx11.exe) you'll need the .text portion of PE. 
Easiest way I've found is to use the nuget package PeNet to get the pointer to the raw data of the header and the .text header's address.
Then load the file into a Memory<byte> and slice out the .text portion. Can also be done for .rdata/.data etc.

```cs
Memory<byte> file = new Memory<byte>(File.ReadAllBytes(@"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\ffxiv_dx11.exe"));
PeFile peHeader = new PeFile(file.Span.ToArray());
var text = peHeader.ImageSectionHeaders[0];
var WitchHuntSearcher = new WitchHunt(file.Slice((int) text.PointerToRawData, (int) text.SizeOfRawData), new IntPtr(text.VirtualAddress));
IntPtr result = WitchHuntSearcher.Search("48 8D 05 ?? ?? ?? ?? 48 C7 43 ?? ?? ?? ?? ?? 48 8D 4B ?? 48 89 03 66 C7 43 ?? ?? ?? Add 3 TraceRelative");
```
Though you'd want to only do everything above WitchHuntSearcher.Search() once to prevent loading the file into memory multiple times.

#### From a process
Using the pattern searcher on a process's memory is very easy. This example is assuming it's be run while injected in the process.

```cs
var bytes = new byte[Process.GetCurrentProcess().MainModule.ModuleMemorySize];
IntPtr outBytes = IntPtr.Zero;
var read = ReadProcessMemory(
                             Process.GetCurrentProcess().Handle,
                             new UIntPtr((ulong)Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64()),
                             bytes,
                             new UIntPtr((uint)Process.GetCurrentProcess().MainModule.ModuleMemorySize),
                             outBytes);

var WitchHuntSearcher = new WitchHunt(bytes, Process.GetCurrentProcess().MainModule.BaseAddress);
```

The constructor for WitchHunt can take a byte[], Span<byte> or Memory<byte> and their ReadOnly versions
