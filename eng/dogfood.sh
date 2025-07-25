#!/usr/bin/env bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  scriptroot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$scriptroot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
scriptroot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

. "$scriptroot/common/tools.sh"
. "$scriptroot/configure-toolset.sh"
InitializeToolset
. "$scriptroot/restore-toolset.sh"

ReadGlobalVersion "dotnet"
dotnet_sdk_version=$_ReadGlobalVersion

export SDK_REPO_ROOT="$repo_root"

testDotnetRoot="$artifacts_dir/bin/redist/$configuration/dotnet"
testDotnetVersion=$(ls $testDotnetRoot/sdk)
export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR="$testDotnetRoot/sdk/$testDotnetVersion/Sdks"
export MicrosoftNETBuildExtensionsTargets="$artifacts_dir/bin/$configuration/Sdks/Microsoft.NET.Build.Extensions/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets"

export PATH=$testDotnetRoot:$PATH
export DOTNET_ROOT=$testDotnetRoot
export DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0
export PS1="(dogfood) $PS1"
export DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT="true"