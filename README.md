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

1. Uncomment `#defines` in Program.cs
2. Build and run.

Example for 100,000,000 records on a 8GB 8 core machine:
```
tmaj@myhost:~/dev/1brc.tmaj/1brc.tmaj$ time dotnet run /home/tmaj/dev/1brc/measurements.txt 
Getting len: 00:00:00.0018206
Array alloc:  00:00:00.0000777
Read into the array:  00:00:00.4272838
Data parsing and partitioning:  00:00:01.2646818
numMeas=100,000,000
{<output_here>}
Last bit:  00:00:00.0105614
Total elapsed: 00:00:01.7231063

real    0m3.496s
user    0m10.358s
sys     0m2.351s
```
## TODOs

1. Show results for 1,000,000,000
2. Run with NativeAOT
   1. Run
   2. Update Readme with `dotnet publish` instructions.

