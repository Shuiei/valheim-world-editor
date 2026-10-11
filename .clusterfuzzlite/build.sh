#!/bin/bash -eu
# The .NET fuzz targets (tests/Fuzz/Program.cs). ClusterFuzzLite has no .NET of its own: each target is
# libfuzzer-dotnet (libFuzzer's driver for a .NET program) compiled with tests/Fuzz/target.cc, which
# points it at the program and the target's name.
TARGETS="blueprint homestead zdo inventory dungeon zonedb snapshot chunk png"

# Self-contained: the runner image has no .NET. The game data the tests use goes beside it.
dotnet publish tests/Fuzz/Fuzz.csproj -c Release -r linux-x64 --self-contained -o $OUT/fuzz
mkdir -p $OUT/fuzz/fixtures
cp tests/fixtures/*.json $OUT/fuzz/fixtures/
# Coverage for the editor's own code only.
/opt/sharpfuzz/sharpfuzz $OUT/fuzz/ValheimWorldEditor.Core.dll

$CXX $CXXFLAGS -fsanitize=fuzzer -DLLVMFuzzerInitialize=DriverInitialize -c $SRC/libfuzzer-dotnet/libfuzzer-dotnet.cc -o $WORK/driver.o
for target in $TARGETS; do
  $CXX $CXXFLAGS -fsanitize=fuzzer -DFUZZ_TARGET=$target tests/Fuzz/target.cc $WORK/driver.o -o $OUT/$target
  (cd tests/Fuzz/seeds/$target && zip -q -r $OUT/${target}_seed_corpus.zip .)
  cp tests/Fuzz/dict/$target.dict $OUT/$target.dict
done
