#!/bin/sh

set -x

rm -rf dist build
mkdir dist
dotnet publish -o dist || exit 1
mkdir build

cd build

case "$(uname -s)" in

   Linux)
     cmake -DCMAKE_INSTALL_PREFIX=../dist -DCMAKE_BUILD_TYPE=Release ../FuseNativeAdapter
     make install || exit 1
     ;;

   CYGWIN*|MINGW32*|MSYS*|MINGW*)
     cmake -DCMAKE_INSTALL_PREFIX=../dist/ ../FuseNativeAdapter
     cmake --build . --target install --config Release || exit 1
     ;;

   *)
     echo 'Unknown OS' 
     ;;
esac