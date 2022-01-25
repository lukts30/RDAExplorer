# Building

Requires cmake and fuse3 ([winfsp](https://github.com/billziss-gh/winfsp) on Windows).

## Linux


```console
mkdir build && cd build
cmake -DCMAKE_INSTALL_PREFIX=../../bin/Debug/net6.0/ -DCMAKE_BUILD_TYPE=Release ..
make install
```

## Windows

```console
mkdir build && cd build
cmake -DCMAKE_INSTALL_PREFIX=../../bin/Debug/net6.0/ ..
cmake --build . --target install --config Release
```