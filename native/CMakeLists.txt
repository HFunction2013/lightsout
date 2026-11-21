cmake_minimum_required(VERSION 3.15)
project(lightsout C)

set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
  set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
endif()

# Shared solver library: lightsout.dll / liblightsout.so / liblightsout.dylib
add_library(lightsout SHARED lightsout.c)
set_target_properties(lightsout PROPERTIES
  OUTPUT_NAME lightsout
  C_VISIBILITY_PRESET hidden
  POSITION_INDEPENDENT_CODE ON
)
target_include_directories(lightsout PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})

if(MSVC)
  target_compile_options(lightsout PRIVATE /O2 /W4 /utf-8)
else()
  target_compile_options(lightsout PRIVATE -O3 -Wall -Wextra)
endif()

option(LIGHTSOUT_BUILD_TESTS "Build the solver self test" ON)
if(LIGHTSOUT_BUILD_TESTS)
  add_executable(selftest selftest.c)
  target_link_libraries(selftest PRIVATE lightsout)
  if(MSVC)
    target_compile_options(selftest PRIVATE /O2 /W4 /utf-8)
  else()
    target_compile_options(selftest PRIVATE -O2 -Wall -Wextra)
  endif()
endif()
