cmake_minimum_required(VERSION 3.22)
project(TrackVisualizer VERSION 0.1.0 LANGUAGES C CXX)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
include(FetchContent)
FetchContent_Declare(JUCE GIT_REPOSITORY https://github.com/juce-framework/JUCE.git
    GIT_TAG 29396c22c93392d6738e021b83196283d6e4d850 GIT_SHALLOW FALSE)
FetchContent_MakeAvailable(JUCE)
juce_add_plugin(TrackVisualizer
    COMPANY_NAME "Visualizer" BUNDLE_ID "com.visualizer.trackvisualizer"
    PLUGIN_MANUFACTURER_CODE Vzlr PLUGIN_CODE Tviz
    FORMATS VST3 PRODUCT_NAME "Track Visualizer"
    IS_SYNTH FALSE NEEDS_MIDI_INPUT FALSE NEEDS_MIDI_OUTPUT FALSE IS_MIDI_EFFECT FALSE
    EDITOR_WANTS_KEYBOARD_FOCUS FALSE COPY_PLUGIN_AFTER_BUILD FALSE
    VST3_CATEGORIES Fx Analyzer)
target_sources(TrackVisualizer PRIVATE Source/PluginProcessor.cpp Source/PluginEditor.cpp)
target_compile_definitions(TrackVisualizer PUBLIC JUCE_WEB_BROWSER=0 JUCE_USE_CURL=0 JUCE_VST3_CAN_REPLACE_VST2=0)
target_link_libraries(TrackVisualizer PRIVATE juce::juce_audio_utils juce::juce_dsp
    PUBLIC juce::juce_recommended_config_flags juce::juce_recommended_warning_flags)
option(VISUALIZER_BUILD_TESTS "Build the VST3 host smoke test" ON)
if(VISUALIZER_BUILD_TESTS)
    juce_add_console_app(VisualizerHostCheck PRODUCT_NAME "Visualizer Host Check")
    target_sources(VisualizerHostCheck PRIVATE Tests/HostCheck.cpp)
    target_compile_definitions(VisualizerHostCheck PRIVATE JUCE_PLUGINHOST_VST3=1 JUCE_MODAL_LOOPS_PERMITTED=1 JUCE_WEB_BROWSER=0 JUCE_USE_CURL=0)
    target_link_libraries(VisualizerHostCheck PRIVATE juce::juce_audio_utils juce::juce_recommended_config_flags)
    enable_testing()
    add_test(NAME VST3HostCheck COMMAND VisualizerHostCheck "${CMAKE_CURRENT_BINARY_DIR}/TrackVisualizer_artefacts/$<CONFIG>/VST3/Track Visualizer.vst3")
endif()

if(VISUALIZER_BUILD_TESTS)
    juce_add_console_app(VisualizerWindowCheck PRODUCT_NAME "Visualizer Window Check")
    target_sources(VisualizerWindowCheck PRIVATE Tests/WindowCheck.cpp Source/PluginProcessor.cpp Source/PluginEditor.cpp)
    target_include_directories(VisualizerWindowCheck PRIVATE Source)
    target_compile_definitions(VisualizerWindowCheck PRIVATE JUCE_MODAL_LOOPS_PERMITTED=1 JUCE_WEB_BROWSER=0 JUCE_USE_CURL=0)
    target_link_libraries(VisualizerWindowCheck PRIVATE juce::juce_audio_utils juce::juce_dsp juce::juce_recommended_config_flags)
    add_test(NAME WindowCheck COMMAND VisualizerWindowCheck)
endif()