#include "PluginProcessor.h"
#include <iostream>
#include <stdexcept>

void check(bool ok, const char* text) { if (!ok) throw std::runtime_error(text); }
int main(int argc, char** argv)
{
    try
    {
        juce::ScopedJuceInitialiser_GUI gui;
        const int initialWindows = juce::Desktop::getInstance().getNumComponents();
        {
            VisualizerProcessor first, second;
            juce::AudioProcessor::TrackProperties a, b; a.name = "Drums"; b.name = "Bass";
            first.updateTrackProperties(a); second.updateTrackProperties(b);
            first.prepareToPlay(48000, 512); second.prepareToPlay(48000, 512);
            std::unique_ptr<juce::AudioProcessorEditor> editor1(first.createEditorIfNeeded()), editor2(second.createEditorIfNeeded());
            auto find = [](const juce::String& name) -> juce::DocumentWindow*
            {
                auto& desktop = juce::Desktop::getInstance();
                for (int i = 0; i < desktop.getNumComponents(); ++i)
                    if (auto* w = dynamic_cast<juce::DocumentWindow*>(desktop.getComponent(i)))
                        if (w->getName() == name) return w;
                return nullptr;
            };
            auto* drums = find("Track Visualizer - Drums"); auto* bass = find("Track Visualizer - Bass");
            check(drums != nullptr && bass != nullptr && drums != bass, "Independent track windows missing");
            first.editorBeingDeleted(editor1.get()); editor1.reset();
            second.editorBeingDeleted(editor2.get()); editor2.reset();
            check(drums->isVisible() && bass->isVisible(), "Closing host panels closed floating windows");
            auto set = [](VisualizerProcessor& p, const char* id, float v)
            { auto* parameter = p.state.getParameter(id); parameter->setValueNotifyingHost(parameter->convertTo0to1(v)); };
            set(first, "shape", 7); set(second, "shape", 8);
            check(first.value("shape") != second.value("shape"), "Settings leaked across instances");
            juce::AudioBuffer<float> tone(2, 512); juce::MidiBuffer midi;
            for (int shape = 0; shape < 10; ++shape)
            for (int effect = 0; effect < 4; ++effect)
            {
                set(first, "shape", (float)shape); set(first, "crt", (float)effect); set(first, "rotation", (float)(shape % 3));
                for (int block = 0; block < 8; ++block)
                {
                    for (int c = 0; c < 2; ++c) for (int i = 0; i < 512; ++i) tone.setSample(c, i, 0.3f * std::sin((float)(i + block * 512) * 0.0576f));
                    first.processBlock(tone, midi); second.processBlock(tone, midi);
                }
                juce::MessageManager::getInstance()->runDispatchLoopUntil(50);
                auto* content = drums->getContentComponent();
                auto image = content->createComponentSnapshot(content->getLocalBounds());
                check(image.isValid(), "Visualization snapshot failed");
                int lit = 0;
                for (int y = 55; y < image.getHeight() - 224; y += 2)
                    for (int x = 20; x < image.getWidth() - 20; x += 2)
                        if (image.getPixelAt(x, y).getGreen() > 80) ++lit;
                check(lit > 0, "No visible visualization geometry");
                if (shape == 7 && effect == 3 && argc > 1)
                {
                    juce::FileOutputStream stream(juce::File(juce::String::fromUTF8(argv[1])));
                    check(stream.openedOk(), "Cannot save screenshot"); juce::PNGImageFormat png; png.writeImageToStream(image, stream);
                }
            }
            drums->setSize(780, 550);
            auto* panel = drums->getContentComponent();
            for (int i = 0; i < panel->getNumChildComponents(); ++i)
                check(panel->getLocalBounds().contains(panel->getChildComponent(i)->getBounds()), "Control clipped at minimum size");
            drums->closeButtonPressed(); juce::MessageManager::getInstance()->runDispatchLoopUntil(60);
            check(!first.editorActive.load() && second.editorActive.load(), "Closing one window affected another track");
            first.openVisualizerWindow(); juce::MessageManager::getInstance()->runDispatchLoopUntil(60);
            check(first.editorActive.load() && find("Track Visualizer - Drums") == drums, "Reopen created duplicate or inactive window");
            std::cout << "PASS: two track windows, independent parameters, persistence after host-panel close, all shapes/effects, resize, close/reopen\n";
        }
        check(juce::Desktop::getInstance().getNumComponents() == initialWindows, "Windows leaked after plug-in destruction");
        std::cout << "PASS: removing instances closes their windows\n";
        return 0;
    }
    catch (const std::exception& e) { std::cerr << "FAIL: " << e.what() << '\n'; return 1; }
}
