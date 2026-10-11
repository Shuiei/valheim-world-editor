// One fuzz target as a libFuzzer binary of its own: libfuzzer-dotnet (the native driver that runs a
// .NET program under libFuzzer), started as if given --target_path=<this folder>/fuzz/Fuzz and
// --target_arg=FUZZ_TARGET. ClusterFuzzLite checks that each target is such a binary (built with the
// sanitizer and libFuzzer's coverage), not a script.
#include <limits.h>
#include <libgen.h>
#include <string.h>
#include <unistd.h>
#include <string>
#include <vector>

extern "C" int DriverInitialize(int *argc, char ***argv);

#define STR2(x) #x
#define STR(x) STR2(x)

extern "C" int LLVMFuzzerInitialize(int *argc, char ***argv)
{
    static std::string path, arg;
    static std::vector<char *> args;
    char self[PATH_MAX];
    ssize_t n = readlink("/proc/self/exe", self, sizeof self - 1);
    self[n > 0 ? n : 0] = 0;
    path = std::string("--target_path=") + dirname(self) + "/fuzz/Fuzz";
    arg = std::string("--target_arg=") + STR(FUZZ_TARGET);
    args.assign(*argv, *argv + *argc);
    args.push_back(path.data());
    args.push_back(arg.data());
    args.push_back(nullptr);
    *argc = static_cast<int>(args.size()) - 1;
    *argv = args.data();
    return DriverInitialize(argc, argv);
}
