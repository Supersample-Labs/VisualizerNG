#include "PluginProcessor.h"
#include "PluginEditor.h"

juce::StringArray VisualizerProcessor::shapes()
{
    return { "Oscilloscope", "Circle", "Triangle", "Square", "Pentagon", "Hexagon", "Octagon", "Star", "Spectrum", "Radial spectrum" };
}
juce::StringArray VisualizerProcessor::palettes()
{
    return { "Lagoon", "Electric blue", "Neon violet", "Sunset", "Lime" };
}
juce::AudioProcessorValueTreeState::ParameterLayout VisualizerProcessor::parameters()
{
    juce::AudioProcessorValueTreeState::ParameterLayout p;
    auto choice = [&](const char* id, const char* name, juce::StringArray choices)
    { p.add(std::make_unique<juce::AudioParameterChoice>(juce::ParameterID(id, 1), name, choices, 0)); };
    auto number = [&](const char* id, const char* name, float low, float high, float step, float initial)
    { p.add(std::make_unique<juce::AudioParameterFloat>(juce::ParameterID(id, 1), name, juce::NormalisableRange<float>(low, high, step), initial)); };
    choice("shape", "Shape", shapes());
    choice("palette", "Colors", palettes());
    choice("rotation", "Rotation", { "Off", "2D spin", "3D spin" });
    choice("crt", "CRT effects", { "Off", "Snow", "Interlace", "Snow + Interlace" });
    number("angle", "Angle", 0, 360, 1, 0);
    number("tilt", "Tilt", -180, 180, 1, 0);
    number("turn", "Turn", -180, 180, 1, 0);
    number("snow", "Snow intensity", 0, 1, 0.01f, 0.3f);
    number("scan", "Interlace intensity", 0, 1, 0.01f, 0.5f);
    number("sensitivity", "Sensitivity", 0.4f, 4, 0.1f, 1.4f);
    number("smoothing", "Smoothing", 0, 0.96f, 0.01f, 0.78f);
    return p;
}
VisualizerProcessor::VisualizerProcessor()
    : AudioProcessor(BusesProperties().withInput("Input", juce::AudioChannelSet::stereo(), true)
                                     .withOutput("Output", juce::AudioChannelSet::stereo(), true)),
      state(*this, nullptr, "TrackVisualizerState", parameters()) {}

bool VisualizerProcessor::isBusesLayoutSupported(const BusesLayout& layout) const
{
    const auto output = layout.getMainOutputChannelSet();
    return (output == juce::AudioChannelSet::mono() || output == juce::AudioChannelSet::stereo())
        && output == layout.getMainInputChannelSet();
}
void VisualizerProcessor::processBlock(juce::AudioBuffer<float>& buffer, juce::MidiBuffer&)
{
    // Analysis is a tap only: never write to the host's audio. No allocation,
    // locks, FFT, drawing, or UI calls on the audio thread.
    if (!editorActive.load(std::memory_order_relaxed) || buffer.getNumChannels() == 0 || buffer.getNumSamples() == 0) return;
    int channel = 0;
    if (buffer.getNumChannels() > 1 && buffer.getRMSLevel(1, 0, buffer.getNumSamples()) > buffer.getRMSLevel(0, 0, buffer.getNumSamples())) channel = 1;
    const auto* input = buffer.getReadPointer(channel);
    int start1, size1, start2, size2;
    fifo.prepareToWrite(buffer.getNumSamples(), start1, size1, start2, size2);
    for (int i = 0; i < size1; ++i) audio[(size_t)(start1 + i)] = std::isfinite(input[i]) ? input[i] : 0;
    for (int i = 0; i < size2; ++i) audio[(size_t)(start2 + i)] = std::isfinite(input[size1 + i]) ? input[size1 + i] : 0;
    fifo.finishedWrite(size1 + size2); // Drop excess analysis samples rather than block the DAW.
}
int VisualizerProcessor::readAudio(float* destination, int maximum)
{
    int start1, size1, start2, size2;
    fifo.prepareToRead(maximum, start1, size1, start2, size2);
    std::copy_n(audio.data() + start1, size1, destination);
    std::copy_n(audio.data() + start2, size2, destination + size1);
    fifo.finishedRead(size1 + size2);
    return size1 + size2;
}
void VisualizerProcessor::getStateInformation(juce::MemoryBlock& block)
{
    if (auto xml = state.copyState().createXml()) copyXmlToBinary(*xml, block);
}
void VisualizerProcessor::setStateInformation(const void* data, int size)
{
    if (auto xml = getXmlFromBinary(data, size))
        if (xml->hasTagName(state.state.getType())) state.replaceState(juce::ValueTree::fromXml(*xml));
}
juce::AudioProcessorEditor* VisualizerProcessor::createEditor() { return new VisualizerEditor(*this); }
juce::AudioProcessor* JUCE_CALLTYPE createPluginFilter() { return new VisualizerProcessor(); }

void VisualizerProcessor::updateTrackProperties(const TrackProperties& properties)
{
    const juce::ScopedLock lock(titleLock); trackName = properties.name.value_or(juce::String());
}
juce::String VisualizerProcessor::windowTitle() const
{
    const juce::ScopedLock lock(titleLock);
    return trackName.isEmpty() ? "Track Visualizer" : "Track Visualizer - " + trackName;
}
VisualizerProcessor::~VisualizerProcessor() { floatingWindow.reset(); }
