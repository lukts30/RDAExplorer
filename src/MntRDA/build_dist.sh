#!/bin/sh

set -x

rm -rf dist build
mkdir dist
dotnet publish -o dist
mkdir build

cd build

case "$(uname -s)" in

   Linux)
     cmake -DCMAKE_INSTALL_PREFIX=../dist -DCMAKE_BUILD_TYPE=Release ../FuseNativeAdapter
     make install
     ;;

   CYGWIN*|MINGW32*|MSYS*|MINGW*)
     cmake -DCMAKE_INSTALL_PREFIX=../dist/ ../FuseNativeAdapter
     cmake --build . --target install --config Release
     ;;

   *)
     echo 'Unknown OS' 
     ;;
esac