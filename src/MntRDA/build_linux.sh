#!/bin/sh

set -x

rm -rf dist build
mkdir dist
dotnet publish --os linux-x64 -o dist
mkdir build
cd build
cmake -DCMAKE_INSTALL_PREFIX=../dist -DCMAKE_BUILD_TYPE=Release ../FuseNativeAdapter
make install