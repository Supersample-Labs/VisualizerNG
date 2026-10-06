#pragma once
#include "PluginProcessor.h"
#include <juce_dsp/juce_dsp.h>

class VisualizerPanel final : public juce::Component, private juce::Timer
{
public:
    explicit VisualizerPanel(VisualizerProcessor&);
    ~VisualizerPanel() override;
    void paint(juce::Graphics&) override;
    void resized() override;
private:
    void timerCallback() override;
    void drawVisualization(juce::Graphics&);
    void buildCrt();
    juce::Point<float> project(float x, float y, bool roundShape) const;
    void updateEnabled();
    VisualizerProcessor& processor;
    juce::LookAndFeel_V4 look;
    juce::ComboBox shape, palette, rotation, crt;
    juce::Slider angle, tilt, turn, snow, scan, sensitivity, smoothing;
    juce::Label shapeLabel, paletteLabel, rotationLabel, crtLabel;
    juce::Label angleLabel, tiltLabel, turnLabel, snowLabel, scanLabel, sensitivityLabel, smoothingLabel;
    juce::TextButton reset { "Reset rotation" };
    using ComboAttachment = juce::AudioProcessorValueTreeState::ComboBoxAttachment;
    using SliderAttachment = juce::AudioProcessorValueTreeState::SliderAttachment;
    std::vector<std::unique_ptr<ComboAttachment>> comboAttachments;
    std::vector<std::unique_ptr<SliderAttachment>> sliderAttachments;
    std::array<float, 32768> incoming {};
    std::array<float, 4096> history {};
    std::array<float, 8192> fftData {};
    std::array<float, 1024> waveform {};
    std::array<float, 64> spectrum {}, peaks {};
    juce::dsp::FFT fft { 12 };
    juce::dsp::WindowingFunction<float> window { 4096, juce::dsp::WindowingFunction<float>::hann };
    size_t writePosition = 0;
    juce::Rectangle<float> viewport;
    juce::Image crtImage;
    juce::Random random;
    double lastTick = 0, time = 0, lastAudio = 0;
    float spin = 0, tiltSpin = 0, turnSpin = 0, level = 0;
    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(VisualizerPanel)
};

class VisualizerEditor final : public juce::AudioProcessorEditor
{
public:
    explicit VisualizerEditor(VisualizerProcessor&);
    ~VisualizerEditor() override;
    void paint(juce::Graphics&) override;
    void resized() override;
private:



    VisualizerProcessor& processor;
    juce::TextButton open { "Open visualizer window" };

    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(VisualizerEditor)
};