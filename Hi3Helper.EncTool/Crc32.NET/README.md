# Note
This repo was originally created by [**force-net**](https://github.com/force-net/Crc32.NET) and some of its functionality like CRC32C was removed for this version of Crc32.NET.

# Crc32.NET

Optimized and fast managed implementation of Crc32 & Crc32C (Castagnoly) algorithms for .NET and .NET Core. 

*(But if you need, I can add native implementation (with transparent .net wrapper) which is twice faster .NET version)*

Installation through Nuget:

````bash
> Install-Package Crc32.NET
````

### Version 1.1.0 Remarks

Initially, this library only support Crc32 checksum, but I found, that there are lack of .NET Core libraries for Crc32 calculation. So, I've added Crc32C (Castagnoli) managed implementation in this library. Other Crc32 variants (like Crc32Q or Crc32K) seems to be unpopular to implement it here.

So, if you need to use Crc32C for .NET Core, you can use this library. But if you only use 'big' .NET frameworks, it is better to use [Crc32C.NET](https://crc32c.angeloflogic.com/) for Crc32C? because it has fast native implementation. 

### Version 1.2.0 Remarks

CRC algorithms has interesting feature: if we calculate it for some data and write result CRC data to end of source data, then calculate data **with** this CRC we'll receive a constant number.
This number is 0x2144DF1C for CRC32 and 0x48674BC7 for CRC32C.

This feature can be used for more convenient CRC usage, e.g. validation of correctness is a simple comparison with constant. There are no requirement to decode CRC value from input array and compare it with calculated data.

So in 1.2.0 version, I've added 2 methods (with overloads) to use this feature:

````csharp
var inputArray = new byte[realDataLength + 4];

// write real data to inputArray
Crc32Algorithm.ComputeAndWriteToEnd(inputArray); // last 4 bytes contains CRC

// transferring data or writing reading, and checking as final operation
if (!Crc32Algorithm.IsValidWithCrcAtEnd(inputArray))
{
    throw new InvalidOperationException("Data was tampered");
}

````

In other words, you should pass some input buffer to calculation function and it will write CRC data at last 4 bytes. After that, when you need validation, you should pass this buffer to validation function and it will return _is data correct_.
 

## Description

This library is port of [Crc32C.NET](https://crc32c.angeloflogic.com/) by Robert Važan but for Crc32 algorithm. Also, this library contains optimizations for managed code, so, it really faster than other Crc32 implementations. 

If you do not not catch the difference, it is *C* (Castagnoli). I recommend to use Crc32C, not usual CRC32, because it can be faster (up to 20GB/s with native CPU implementation) and slightly better in error detection. But if you need exactly Crc32, this library is the best choice.

### Performance

This library has code, which is optimized for .NET (implementation is not dumb copy-paste from google), as result, it is really fast in comparison with other implemenations. 

Library | Speed
--------|-------
[CH.Crc32](https://github.com/tanglebones/ch-crc32) by Cliff Hammerschmidt | 117 MB/s
[Crc32](https://github.com/dariogriffo/Crc32) by Dario Griffo | 401 MB/s
[Klinkby.Checksum](https://github.com/klinkby/klinkby.checksum) by Mads Breusch Klinkby | 400 MB/s
[Data.HashFunction.CRC](https://github.com/brandondahler/Data.HashFunction/) by Brandon Dahler | 206 MB/s
[Dexiom.QuickCrc32](https://github.com/Dexiom/Dexiom.QuickCrc32/) by Jonathan Paré | 364 MB/s
[K4os.Hash.Crc](https://github.com/MiloszKrajewski/K4os.Hash.Crc) by Milosz Krajewski  | 399 MB/s
This library | **1170** MB/s

## Some notes

I thought about making a pull request to [Crc32](https://github.com/dariogriffo/Crc32) library, but it seems, this library was abandoned. Anyway, I implement my library to be fully compatible with Crc32 library. And you can switch from Crc32 library to this.

Api interface was taken from [Crc32C.NET](https://crc32c.angeloflogic.com/) library. It is very handy for using in applications.

## License

[MIT](https://github.com/force-net/Crc32.NET/blob/develop/LICENSE) license
