cmake_minimum_required(VERSION 3.10)


project (FuseNativeAdapter, LANGUAGES C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED True)

set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

# if (MSVC)
#     add_compile_options(/W4 /WX)
# else()
#     add_compile_options(-Wall -Wextra -pedantic -Werror)
# endif()

option(BUILD_SHARED_LIBS "Build using shared libraries" ON)
add_library(FuseNativeAdapter FuseNativeAdapter.c)
# add_executable(FuseNativeAdapter FuseNativeAdapter.c)

if (NOT CYGWIN AND WIN32)
    set_target_properties(FuseNativeAdapter PROPERTIES LINK_FLAGS "/DELAYLOAD:winfsp-x64.dll")
    target_include_directories(FuseNativeAdapter PRIVATE "$ENV{ProgramFiles\(x86\)}\\WinFsp\\inc\\")
    target_include_directories(FuseNativeAdapter PRIVATE "$ENV{ProgramFiles\(x86\)}\\WinFsp\\inc\\fuse3")
    target_link_libraries(FuseNativeAdapter "$ENV{ProgramFiles\(x86\)}\\WinFsp\\lib\\winfsp-x64.lib")
else()
    find_package(PkgConfig REQUIRED)
	pkg_check_modules(FUSE REQUIRED fuse3)
    target_include_directories(FuseNativeAdapter PUBLIC ${FUSE_INCLUDE_DIRS})
    target_compile_options(FuseNativeAdapter PUBLIC ${FUSE_CFLAGS_OTHER})
    target_link_libraries(FuseNativeAdapter ${FUSE_LIBRARIES})
endif()


install(TARGETS FuseNativeAdapter DESTINATION .)
