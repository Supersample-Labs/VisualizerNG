#pragma once
#include <juce_audio_utils/juce_audio_utils.h>
#include <array>
#include <atomic>

class VisualizerProcessor final : public juce::AudioProcessor
{
public:
    VisualizerProcessor();
    ~VisualizerProcessor() override;
    void openVisualizerWindow();
    const juce::String getName() const override { return "Track Visualizer"; }
    void prepareToPlay(double rate, int) override { sampleRate.store(rate); }
    void releaseResources() override {}
    bool isBusesLayoutSupported(const BusesLayout&) const override;
    void processBlock(juce::AudioBuffer<float>&, juce::MidiBuffer&) override;
    bool hasEditor() const override { return true; }
    juce::AudioProcessorEditor* createEditor() override;
    bool acceptsMidi() const override { return false; }
    bool producesMidi() const override { return false; }
    double getTailLengthSeconds() const override { return 0; }
    int getNumPrograms() override { return 1; }
    int getCurrentProgram() override { return 0; }
    void setCurrentProgram(int) override {}
    const juce::String getProgramName(int) override { return {}; }
    void changeProgramName(int, const juce::String&) override {}
    void getStateInformation(juce::MemoryBlock&) override;
    void setStateInformation(const void*, int) override;
    int readAudio(float* destination, int maximum);
    float value(const char* id) const { return state.getRawParameterValue(id)->load(); }
    juce::AudioProcessorValueTreeState state;
    std::atomic<bool> editorActive { false };
    std::atomic<double> sampleRate { 48000.0 };
    void updateTrackProperties(const TrackProperties&) override;
    juce::String windowTitle() const;
    static juce::StringArray shapes();
    static juce::StringArray palettes();
private:
    static juce::AudioProcessorValueTreeState::ParameterLayout parameters();
    std::unique_ptr<juce::DocumentWindow> floatingWindow;
    mutable juce::CriticalSection titleLock;
    juce::String trackName;
    static constexpr int capacity = 32768;
    juce::AbstractFifo fifo { capacity };
    std::array<float, capacity> audio {};
    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(VisualizerProcessor)
};
