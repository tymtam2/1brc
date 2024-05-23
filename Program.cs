#define LOG
#define LOG1


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// 200,000,000, 8Gb machine: 
// Getting len: 00:00:00.0016930
// Arrays alloc:  00:00:00.0014529
// Read into the array:  00:00:01.8042291
// Data parsing and partitioning:  00:00:02.0713979
// numMeas=200,000,000
// {Abha= ...}
// Last bit:  00:00:00.0117738
// Total elapsed: 00:00:03.8998091

// real    0m5.933s
// user    0m17.582s
// sys     0m4.541s

// real    0m7.343s
// user    0m18.343s
// sys     0m4.589s

// real    0m5.491s
// user    0m17.816s
// sys     0m3.851s


// 1,000,000,000 on 32GB
// Getting len: 00:00:00.0001106
// Arrays alloc:  00:00:00.0098801
// Read into the array:  00:00:01.5287347
// Data parsing and partitioning:  00:00:13.0448311
// numMeas=1,000,000,000
// {Data here}
// Last bit:  00:00:00.0009655
// Total elapsed: 00:00:14.5889054
//
// real	0m15.209s
// user	1m32.825s
// sys	0m12.214s

class TestClass
{

  private static readonly object _lock = new object();
  static void Main(string[] args)
  {
    var logicalProcessorsCount = Environment.ProcessorCount;

    var swTot = Stopwatch.StartNew();
    var sw = Stopwatch.StartNew();
    var path = args[0];
    int numMeas = 0;
    long length = 0;

    using (var fs = File.OpenRead(path))
    {
      length = fs.Length; // 0:00:03.358 or 00:00:00.0018295
    }

#if LOG
    var elapsed = sw.Elapsed;
    Console.WriteLine($"Getting len: {elapsed}");
    sw.Restart();
#endif


    var nChunks = logicalProcessorsCount;
    var maxDegreeOfParallelism = logicalProcessorsCount;
    int chunk_len = (int)(length / nChunks);

    byte[][] buffers = new byte[nChunks][]; // Arrays larger than 2GB are not supported. 
    for (int iChunk = 0; iChunk < nChunks; iChunk++)
    {
      long arraySize = (iChunk == (nChunks - 1)) ? (length - (iChunk * (long)chunk_len)) : (chunk_len+6); // + 6 so that we can read unfinished measurements without having to jump the chunks
      buffers[iChunk] = new byte[arraySize];
    }

    //byte[] buffer = (byte[])Array.CreateInstance(typeof(byte), length); // Arrays larger than 2GB are not supported. 

#if LOG
    elapsed = sw.Elapsed;
    Console.WriteLine($"Arrays alloc:  {elapsed}");
    sw.Restart();
#endif

    {
      Parallel.For(
        fromInclusive: 0,
        toExclusive: nChunks,
        parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
        body: i =>
        {
          using var fs = File.OpenRead(path);

          long fsStart = i * (long)chunk_len;
          int aStart = 0;
          int bytes_to_read = (int)((i == (nChunks - 1)) ? (length - (long)i * (long)chunk_len) : (chunk_len+6));// + 6 so that we can read unfinished measurements without having to jump the chunks
          byte[] buffer = buffers[i];

          int count = (int)bytes_to_read;
          int bytesRead;

          fs.Seek(fsStart, SeekOrigin.Begin);
          while (count > 0 && ((bytesRead = fs.Read(buffer, offset: aStart, count: count)) > 0))
          {
            bytes_to_read -= bytesRead;
            aStart += bytesRead;
            count = (int)Math.Min(count, bytes_to_read);

          }
        });
    }
#if LOG
    elapsed = sw.Elapsed;
    Console.WriteLine($"Read into the array:  {elapsed}");
    sw.Restart();
#endif

    Dictionary<int, LocationData> dict = [];
    const byte NewLine = (byte)'\n';

    {

      Parallel.For(
        fromInclusive: 0,
        toExclusive: nChunks,
        parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
        body: iChunk =>
        {
           //Console.Write($"[{Thread.CurrentThread.ManagedThreadId}] ");
          ReadOnlySpan<byte> bytes = buffers[iChunk];
          byte[] buffer = buffers[iChunk];
          int start = 0;
          bool isLastChunk = iChunk == (nChunks - 1);
          int bytes_to_read = isLastChunk ? (int)(length - (long)iChunk * (long)chunk_len) : chunk_len;

          int chunk_end = start + bytes_to_read;

          int my_meas_n = 0;

          // Hamburg;12.0\nBulawayo;8.9\nPalembang;38.8

          Dictionary<int, LocationData> localDict = [];

          bool go = iChunk == 0 || buffers[iChunk - 1][chunk_len - 1] == NewLine;
          while (!go)
          {
            if (buffer[start] == NewLine) go = true;
            start++;
          }

          var meas_start = start;
          int i = start;
          int measurement_end = start;
          LocationData? locationData;
          while (i < chunk_end)
          {
            if (buffer[i] != (byte)';') i++;
            else
            {
              var locationNameBytes = bytes[meas_start..i];

              if (buffer[i + 4] == NewLine) measurement_end = i + 3;
              else if (buffer[i + 5] == NewLine) measurement_end = i + 4;
              else if (buffer[i + 6] == NewLine) measurement_end = i + 5;
              else throw new Exception("No end?");

              var temperature = ParseTemperature(buffer, i + 1, measurement_end);

#if DEBUG
              // var tempRawish = float.Parse(tempBytes); // This takes 50% of processing
              // Int16 temp2 = (Int16)(tempRawish * 10);
              // if (temp != temp2)
              // {
              //   Console.WriteLine($"{temp} vs {temp2} :(");
              //   return;
              // }
#endif

              var hash = ComputeHash(locationNameBytes);
              localDict.TryGetValue(hash, out locationData);
              if (locationData != null)
              {
                locationData.Count++;
                locationData.Sum += temperature;
                if (locationData.Max < temperature) locationData.Max = temperature;
                if (locationData.Min > temperature) locationData.Min = temperature;
              }
              else
              {
                LocationData ld = new(name: locationNameBytes.ToArray(), count: 1, sum: temperature, max: temperature, min: temperature);
                localDict.Add(hash, ld);
              }
              my_meas_n++;

              meas_start = measurement_end + 2; // 1: new line 2: next measurement 
              i = meas_start + 3; // Not station names shorter than 3 letters, right? 
            }
          }

          // Process measurements which finish after the chunk's end
          if (!isLastChunk) // but there is no beyond the last chunk 
          {
            if (meas_start < chunk_end) // = only process measurements that started in our chunk 
            {
              i %= chunk_len;
              var nextBuffer = buffers[iChunk + 1];
              while (nextBuffer[i] != (byte)';') i++;

              if (nextBuffer[i + 4] == NewLine) measurement_end = i + 3;
              else if (nextBuffer[i + 5] == NewLine) measurement_end = i + 4;
              else if (nextBuffer[i + 6] == NewLine) measurement_end = i + 5;
              else throw new Exception("No end?");

              var x = buffer[meas_start..chunk_end];
              var y = nextBuffer[0..(measurement_end + 1)];
              var z = new byte[x.Length + y.Length];
              x.CopyTo(z, 0);
              y.CopyTo(z, x.Length);

              var str = System.Text.Encoding.Default.GetString(z);

              var lockEndPlus1 = ((chunk_end - meas_start) + i);
              var locationNameBytes = z[0..lockEndPlus1];
              str = System.Text.Encoding.Default.GetString(locationNameBytes);

              var temperatureA = z[(lockEndPlus1 + 1)..z.Length];
              str = System.Text.Encoding.Default.GetString(temperatureA);

              var temp = ParseTemperature(temperatureA, 0, temperatureA.Length - 1);

#if DEBUG
              // var tempRawish = float.Parse(tempBytes); // This takes 50% of processing
              // Int16 temp2 = (Int16)(tempRawish * 10);
              // if (temp != temp2)
              // {
              //   Console.WriteLine($"{temp} vs {temp2} :(");
              //   return;
              // }
#endif

              var hash = ComputeHash(locationNameBytes);
              localDict.TryGetValue(hash, out locationData);
              if (locationData != null)
              {
                locationData.Count++;
                locationData.Sum += temp;
                if (locationData.Max < temp) locationData.Max = temp;
                if (locationData.Min > temp) locationData.Min = temp;
              }
              else
              {
                LocationData ld = new(name: locationNameBytes.ToArray(), count: 1, sum: temp, max: temp, min: temp);
                localDict.Add(hash, ld);
              }

              my_meas_n++;
            }
          }

          lock (_lock)
          {
            LocationData? existing;
            foreach (var x in localDict)
            {
              var v = x.Value;
              dict.TryGetValue(x.Key, out existing);
              if (existing != null)
              {
                existing.Count += v.Count;
                existing.Sum += v.Sum;
                if (existing.Max < v.Max) existing.Max = v.Max;
                if (existing.Min > v.Min) existing.Min = v.Min;
              }
              else
              {
                dict.Add(x.Key, v);
              }
            }
          }

          Interlocked.Add(ref numMeas, my_meas_n);
        });
    }

#if LOG
    elapsed = sw.Elapsed;
    Console.WriteLine($"Data parsing and partitioning:  {elapsed}");
    sw.Restart();

    Console.WriteLine($"numMeas={numMeas:n0}");
#endif

    var processed = dict.Select(kvp => new
    {
      city = System.Text.Encoding.UTF8.GetString(kvp.Value.Name),
      min = (float)(kvp.Value.Min) / 10,
      mean = ((float)(kvp.Value.Sum) / kvp.Value.Count) / 10,
      max = (float)(kvp.Value.Max) / 10
    })
      .OrderBy(x => x.city, StringComparer.Ordinal) // Ordinal makes Abha appear before Abéché
      .Select(x => $"{x.city}={x.min:F1}/{x.mean:F1}/{x.max:F1}");


    // Example output from https://github.com/gunnarmorling/1brc:
    // {Abha=-23.0/18.0/59.2, Abidjan=-16.2/26.0/67.3, Abéché=-10.0/29.4/69.0,
    var output = string.Join(", ", processed);
    Console.WriteLine($"{{{output}}}");
#if LOG 
    elapsed = sw.Elapsed;
    Console.WriteLine($"Last bit:  {elapsed}");
    sw.Restart();
#endif

#if LOG1
    Console.WriteLine($"Total elapsed: {swTot.Elapsed}");
#endif
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private static Int16 ParseTemperature(byte[] a, int start, int end)
  {
    // Four cases
    // 2.3
    // 12.3
    // -2.3
    // -12.3
    var length = end - start + 1;

    if (a[start] == (byte)'-')
    {
      if (length == 5)
      {
        // -12.3
        return (Int16)(-((a[start + 1 + 0] - 48) * 100 + (a[start + 1 + 1] - 48) * 10 + (a[start + 1 + 3] - 48)));
      }
      else
      {
        // - 2.3
        return (Int16)(-((a[start + 1 + 0] - 48) * 10 + (a[start + 1 + 2] - 48)));
      }
    }
    else
    {
      if (length == 4)
      {
        // 12.3
        return (Int16)((a[start + 0] - 48) * 100 + (a[start + 1] - 48) * 10 + (a[start + 0 + 3] - 48));
      }
      else
      {
        // 2.3
        return (Int16)((a[start + 0] - 48) * 10 + (a[start + 0 + 2] - 48));
      }
    }
    throw new Exception("Failed parsing temperature");
  }


  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static int ComputeHash(ReadOnlySpan<byte> data)
  // by Drew Noakes 
  // from https://stackoverflow.com/questions/16340/how-do-i-generate-a-hashcode-from-a-byte-array-in-c#16381
  {
    unchecked
    {
      const int p = 16777619;
      int hash = (int)2166136261;

      for (int i = 0; i < data.Length; i++)
        hash = (hash ^ data[i]) * p;

      return hash;
    }
  }
}


class LocationData
{


  public byte[] Name; // The example output is not sorted in a dictionary way (h comes before é) which suggest we don't need to 

  public LocationData(byte[] name, Int32 count, long sum, Int16 min, Int16 max)
  {
    Name = name;
    Count = count;
    Sum = sum;
    Min = min;
    Max = max;
  }

  public Int32 Count = 0; // 1. In deci degrees so that we can use Int, not float/single
                          // 2. There are ~400 locations in the example file. 1_000_000_000 / 400 => 2_500_000
  public long Sum;
  public Int16 Min = Int16.MaxValue;
  public Int16 Max = Int16.MinValue;

};

