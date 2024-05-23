# 1brc
This is my attempt at https://github.com/gunnarmorling/1brc using C#.  

## Get measurements

1. Install java
2. Follow https://github.com/gunnarmorling/1brc?tab=readme-ov-file#running-the-challenge
   1. `./mvnw clean verify`
   2. `./create_measurements.sh <number>`


There is also https://huggingface.co/datasets/nietras/1brc.data with example files.
  


## Runnig 
```
cd 1brc.tmaj
dotnet build -c:RELEASE
time dotnet run  /path/to/measurements.txt 
```

### Timing logs for steps

Example for 1,000,000,000 records on a AZure VM:
* Standard D8s v3 (8 vcpus, 32 GiB memory)
* Linux (ubuntu 22.04)
* Intel(R) Xeon(R) CPU E5-2673 v4 @ 2.30GHz
* 32GB
* Iput data from https://huggingface.co/datasets/nietras/1brc.data/tree/main
* Azure's "Premum SSD" 

```
time dotnet run -c Release --project=1brc.tmaj.csproj /home/tmaj/1brc.data/measurements-1000000000.txt > 1brc.tmaj.1_000_000_000.out

real	0m15.209s
user	1m32.825s
sys	0m12.214s
```

The ouput file shows:
```
Getting len: 00:00:00.0031746
Arrays alloc:  00:00:00.0093719
Read into the array:  00:00:01.5494792
Data parsing and partitioning:  00:00:11.4505172
numMeas=1,000,000,000
{<output_here>}
Last bit:  00:00:00.0149148
Total elapsed: 00:00:13.0428714
```

https://github.com/cameronaavik/1brc on the same machine:
```
tmaj@tm1brc:~$ time dotnet run -c Release --project=1brc.cameronaavik/1brc.csproj /home/tmaj/1brc.data/measurements-1000000000.txt > 1brc.cameronaavik.1_000_000_000.out

real	0m6.634s
user	0m33.661s
sys		0m1.929s
```

The outputs data are identical and both agree with [ttps://huggingface.co/datasets/nietras/1brc.data/blob/main/measurements-1000000000.out](https://huggingface.co/datasets/nietras/1brc.data/blob/main/measurements-1000000000.out).

#### Native AOT

```
tmaj@tm1brc:~/1brc.tmaj$ dotnet publish -r linux-x64 -c Release
  Determining projects to restore...
  Restored /home/tmaj/1brc.tmaj/1brc.tmaj.csproj (in 913 ms).
  1brc.tmaj -> /home/tmaj/1brc.tmaj/bin/Release/net8.0/linux-x64/1brc.tmaj.dll
  Generating native code
  1brc.tmaj -> /home/tmaj/1brc.tmaj/bin/Release/net8.0/linux-x64/publish/


tmaj@tm1brc:~$ time /home/tmaj/1brc.tmaj/bin/Release/net8.0/linux-x64/publish/1brc.tmaj /home/tmaj/1brc.data/measurements-1000000000.txt > 1brc.tmaj.1_000_000_000.aot.out 

real	0m14.672s
user	1m42.584s
sys	0m11.906s

```


For reference https://github.com/cameronaavik/1brc on the same machine:
```
tmaj@tm1brc:~/1brc.cameronaavik$ dotnet publish -r linux-x64 -c Release
  Determining projects to restore...
  All projects are up-to-date for restore.
  1brc -> /home/tmaj/1brc.cameronaavik/bin/Release/net8.0/linux-x64/1brc.dll
  Generating native code
  1brc -> /home/tmaj/1brc.cameronaavik/bin/Release/net8.0/linux-x64/publish/

tmaj@tm1brc:~$ time /home/tmaj/1brc.cameronaavik/bin/Release/net8.0/linux-x64/publish/1brc /home/tmaj/1brc.data/measurements-1000000000.txt > 1brc.cameronaavik.1_000_000_000.aot.out 

real	0m4.389s
user	0m32.109s
sys	0m1.546s

```


