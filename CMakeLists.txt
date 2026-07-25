cmake_minimum_required(VERSION 3.20)

if(DEFINED ENV{VCPKG_ROOT} AND NOT DEFINED CMAKE_TOOLCHAIN_FILE)
  set(CMAKE_TOOLCHAIN_FILE "$ENV{VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake" CACHE STRING "")
endif()

project(TaskBuddy CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
if(MSVC)
  add_compile_options(/W4 /EHsc
    $<$<CONFIG:Release>:/MT>
    $<$<CONFIG:Release>:/O2>
    $<$<CONFIG:Debug>:/MTd>
  )
endif()

find_package(imgui CONFIG REQUIRED)

add_executable(TaskBuddy WIN32 src/main.cpp src/AppWindow.cpp src/ProcessEnumerator.cpp src/ProcessOps.cpp src/ServiceMapper.cpp src/ResourceMonitor.cpp src/IconCache.cpp src/imgui_impl_dx11_vendored.cpp)
target_include_directories(TaskBuddy PRIVATE include)
target_link_libraries(TaskBuddy PRIVATE ntdll imgui::imgui d3d11 dxgi shlwapi shell32 advapi32 psapi pdh)




target_sources(TaskBuddy PRIVATE app.manifest)


set_target_properties(TaskBuddy PROPERTIES LINK_FLAGS "/MANIFESTUAC:NO")

