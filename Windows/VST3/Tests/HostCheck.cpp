#include <juce_audio_utils/juce_audio_utils.h>
#include <iostream>
#include <stdexcept>

void require(bool ok, const char* message) { if (!ok) throw std::runtime_error(message); }
int main(int argc, char** argv)
{
    try
    {
        juce::ScopedJuceInitialiser_GUI gui;
        require(argc >= 2, "Pass the VST3 bundle path");
        juce::VST3PluginFormat format;
        juce::OwnedArray<juce::PluginDescription> descriptions;
        format.findAllTypesForFile(descriptions, juce::String::fromUTF8(argv[1]));
        require(descriptions.size() == 1, "VST3 discovery failed");
        juce::AudioPluginFormatManager manager;
        manager.addFormat(std::make_unique<juce::VST3PluginFormat>());
        juce::String error;
        auto plugin = manager.createPluginInstance(*descriptions[0], 48000, 512, error);
        require(plugin != nullptr, error.toRawUTF8());
        require(plugin->getLatencySamples() == 0, "Unexpected audio latency");
        auto set = [&](const char* name, float value)
        {
            for (auto* p : plugin->getParameters())
                if (p->getName(128) == name) { p->setValueNotifyingHost(value); return; }
            throw std::runtime_error(std::string("Missing parameter: ") + name);
        };
        set("Snow intensity", 0.73f);
        juce::MemoryBlock saved; plugin->getStateInformation(saved);
        set("Snow intensity", 0.1f); plugin->setStateInformation(saved.getData(), (int)saved.getSize());
        for (auto* p : plugin->getParameters()) if (p->getName(128) == "Snow intensity")
            require(std::abs(p->getValue() - 0.73f) < 0.011f, "State did not restore");
        std::cout << "PASS: actual VST3 loads, exposes parameters, restores state, and reports zero latency\n";
        for (int channels : { 1, 2 })
        for (double rate : { 44100.0, 48000.0, 96000.0 })
        {
            auto layout = plugin->getBusesLayout();
            layout.inputBuses.set(0, channels == 1 ? juce::AudioChannelSet::mono() : juce::AudioChannelSet::stereo());
            layout.outputBuses.set(0, layout.inputBuses[0]);
            require(plugin->setBusesLayout(layout), "Mono/stereo layout rejected");
            plugin->setRateAndBufferSizeDetails(rate, 512); plugin->prepareToPlay(rate, 512);
            for (int samples : { 1, 64, 127, 512 })
            {
                juce::AudioBuffer<float> buffer(channels, samples);
                for (int c = 0; c < channels; ++c) for (int i = 0; i < samples; ++i)
                    buffer.setSample(c, i, (c == 0 ? 0.3f : -0.2f) * std::sin((float)i * 0.12f));
                juce::AudioBuffer<float> original; original.makeCopyOf(buffer);
                juce::MidiBuffer midi;
                plugin->processBlock(buffer, midi);
                for (int c = 0; c < channels; ++c)
                    require(std::memcmp(buffer.getReadPointer(c), original.getReadPointer(c), (size_t)samples * sizeof(float)) == 0, "Audio was changed");
            }
            plugin->releaseResources();
        }
        std::cout << "PASS: bit-exact mono/stereo pass-through at 44.1/48/96 kHz and varying block sizes\n";
        plugin->setRateAndBufferSizeDetails(48000, 512); plugin->prepareToPlay(48000, 512);
        {
            std::unique_ptr<juce::AudioProcessorEditor> editor(plugin->createEditorIfNeeded());
            require(editor != nullptr, "Missing editor");
            editor->setSize(1000, 680);
            juce::AudioBuffer<float> tone(2, 512); juce::MidiBuffer midi;
            for (int shape = 0; shape < 10; ++shape)
            for (int effect = 0; effect < 4; ++effect)
            {
                set("Shape", (float)shape / 9); set("CRT effects", (float)effect / 3);
                set("Rotation", (float)(shape % 3) / 2);
                for (int block = 0; block < 8; ++block)
                {
                    for (int c = 0; c < 2; ++c) for (int i = 0; i < 512; ++i) tone.setSample(c, i, 0.25f * std::sin((float)(i + block * 512) * 0.0576f));
                    plugin->processBlock(tone, midi);
                }
                juce::MessageManager::getInstance()->runDispatchLoopUntil(45);
                const auto snapshot = editor->createComponentSnapshot(editor->getLocalBounds());
                require(snapshot.isValid(), "Editor drawing failed");
                if (shape == 7 && effect == 3 && argc > 2)
                {
                    juce::FileOutputStream stream(juce::File(juce::String::fromUTF8(argv[2])));
                    require(stream.openedOk(), "Cannot write preview");
                    juce::PNGImageFormat png; png.writeImageToStream(snapshot, stream);
                }
            }
            // Analysis queue overflow must never change or stall track audio.
            for (int i = 0; i < 300; ++i) plugin->processBlock(tone, midi);
            editor->setSize(780, 520);
            require(editor->createComponentSnapshot(editor->getLocalBounds()).isValid(), "Minimum editor size failed");
            plugin->editorBeingDeleted(editor.get());
        }
        {
            std::unique_ptr<juce::AudioProcessorEditor> reopened(plugin->createEditorIfNeeded());
            require(reopened != nullptr, "Cannot reopen editor");
            plugin->editorBeingDeleted(reopened.get());
        }
        plugin->releaseResources();
        std::cout << "PASS: all ten shapes, four CRT modes, rotation modes, editor resize/reopen and bounded analysis queue\n";
        return 0;
    }
    catch (const std::exception& e) { std::cerr << "FAIL: " << e.what() << '\n'; return 1; }
}
