#include "PluginEditor.h"
#include <cmath>

namespace {
constexpr float pi = juce::MathConstants<float>::pi;
constexpr float tau = juce::MathConstants<float>::twoPi;
const juce::Colour primary[] { juce::Colour(0xff40ebdb), juce::Colour(0xff59a3ff), juce::Colour(0xffbd61ff), juce::Colour(0xffff6e3d), juce::Colour(0xff8cf240) };
const juce::Colour accent[] { juce::Colour(0xffa6fff2), juce::Colour(0xffffdb52), juce::Colour(0xffff7ad4), juce::Colour(0xffffdb61), juce::Colour(0xffe6ffa6) };
}
VisualizerPanel::VisualizerPanel(VisualizerProcessor& p) : processor(p)
{
    look.setColour(juce::ResizableWindow::backgroundColourId, juce::Colour(0xff111318));
    look.setColour(juce::ComboBox::backgroundColourId, juce::Colour(0xff252c38));
    look.setColour(juce::Slider::thumbColourId, primary[0]);
    look.setColour(juce::Slider::trackColourId, juce::Colour(0xff348d88));
    setLookAndFeel(&look);
    auto combo = [&](juce::ComboBox& box, juce::Label& label, const char* id, const char* text, juce::StringArray items)
    {
        addAndMakeVisible(box); addAndMakeVisible(label);
        label.setText(text, juce::dontSendNotification);
        box.addItemList(items, 1); box.setName(text);
        comboAttachments.push_back(std::make_unique<ComboAttachment>(processor.state, id, box));
    };
    combo(shape, shapeLabel, "shape", "Visualizer", VisualizerProcessor::shapes());
    combo(palette, paletteLabel, "palette", "Colors", VisualizerProcessor::palettes());
    combo(rotation, rotationLabel, "rotation", "Rotation", { "Off", "2D spin", "3D spin" });
    combo(crt, crtLabel, "crt", "CRT effects", { "Off", "Snow", "Interlace", "Snow + Interlace" });
    auto slider = [&](juce::Slider& control, juce::Label& label, const char* id, const char* text)
    {
        addAndMakeVisible(control); addAndMakeVisible(label);
        label.setText(text, juce::dontSendNotification); control.setName(text);
        control.setSliderStyle(juce::Slider::LinearHorizontal);
        control.setTextBoxStyle(juce::Slider::TextBoxRight, false, 48, 22);
        sliderAttachments.push_back(std::make_unique<SliderAttachment>(processor.state, id, control));
    };
    slider(angle, angleLabel, "angle", "Angle"); slider(tilt, tiltLabel, "tilt", "Tilt");
    slider(turn, turnLabel, "turn", "Turn"); slider(snow, snowLabel, "snow", "Snow");
    slider(scan, scanLabel, "scan", "Interlace");
    slider(sensitivity, sensitivityLabel, "sensitivity", "Sensitivity");
    slider(smoothing, smoothingLabel, "smoothing", "Smoothing");
    addAndMakeVisible(reset);
    reset.onClick = [this]
    {
        for (const auto* id : { "rotation", "angle", "tilt", "turn" })
        {
            auto* parameter = processor.state.getParameter(id);
            parameter->beginChangeGesture(); parameter->setValueNotifyingHost(parameter->convertTo0to1(0)); parameter->endChangeGesture();
        }
        spin = tiltSpin = turnSpin = 0;
    };
    setSize(1000, 680);
    // Drain old analysis from a previous editor instance, then start capturing.
    processor.readAudio(incoming.data(), (int)incoming.size());
    processor.editorActive.store(true);
    lastTick = lastAudio = juce::Time::getMillisecondCounterHiRes() / 1000.0;
    updateEnabled(); startTimerHz(30);
}
VisualizerPanel::~VisualizerPanel()
{
    stopTimer(); processor.editorActive.store(false); setLookAndFeel(nullptr);
}
void VisualizerPanel::updateEnabled()
{
    const int r = (int)processor.value("rotation"), c = (int)processor.value("crt");
    angle.setEnabled(r > 0); tilt.setEnabled(r == 2); turn.setEnabled(r == 2);
    snow.setEnabled(c == 1 || c == 3); scan.setEnabled(c == 2 || c == 3);
}
void VisualizerPanel::timerCallback()
{
    if (!isShowing()) { processor.editorActive.store(false); return; }
    processor.editorActive.store(true);
    if (auto* parent = findParentComponentOfClass<juce::DocumentWindow>()) parent->setName(processor.windowTitle());
    const double now = juce::Time::getMillisecondCounterHiRes() / 1000.0;
    const float dt = (float)juce::jlimit(0.001, 0.1, now - lastTick); lastTick = now; time += dt;
    const int count = processor.readAudio(incoming.data(), (int)incoming.size());
    for (int i = 0; i < count; ++i) { history[writePosition] = incoming[(size_t)i]; writePosition = (writePosition + 1) % history.size(); }
    if (count > 0) lastAudio = now;
    else if (now - lastAudio > 0.15) history.fill(0);
    float energy = 0;
    for (size_t i = 0; i < history.size(); ++i)
    {
        const auto sample = history[(writePosition + i) % history.size()];
        fftData[i] = sample; energy += sample * sample;
        if (i % 4 == 0) waveform[i / 4] = sample;
    }
    const float gain = processor.value("sensitivity");
    const float smooth = std::pow(processor.value("smoothing"), dt * 60.0f);
    level = smooth * level + (1 - smooth) * juce::jlimit(0.0f, 1.0f, std::sqrt(energy / 4096) * gain * 5.5f);
    std::fill(fftData.begin() + 4096, fftData.end(), 0.0f);
    window.multiplyWithWindowingTable(fftData.data(), 4096);
    fft.performFrequencyOnlyForwardTransform(fftData.data());
    const double rate = juce::jmax(8000.0, processor.sampleRate.load());
    const float maxHz = (float)juce::jmin(20000.0, rate * 0.5);
    for (size_t i = 0; i < spectrum.size(); ++i)
    {
        const float low = 20 * std::pow(maxHz / 20, (float)i / 64);
        const float high = 20 * std::pow(maxHz / 20, (float)(i + 1) / 64);
        const int from = juce::jlimit(1, 2047, (int)(low * 4096 / rate));
        const int to = juce::jlimit(from + 1, 2048, (int)std::ceil(high * 4096 / rate));
        float sum = 0;
        for (int bin = from; bin < to; ++bin) sum += std::log10(1 + fftData[(size_t)bin] / 4096 * 18);
        const float target = juce::jlimit(0.0f, 1.0f, sum / (float)(to - from) * gain * 1.8f);
        spectrum[i] = spectrum[i] * smooth + target * (1 - smooth);
        peaks[i] = juce::jmax(spectrum[i], peaks[i] - dt * 0.28f);
    }
    if ((int)processor.value("rotation") > 0) spin = std::fmod(spin + dt * 24, 360.0f);
    if ((int)processor.value("rotation") == 2)
    { tiltSpin = std::fmod(tiltSpin + dt * 17, 360.0f); turnSpin = std::fmod(turnSpin + dt * 29, 360.0f); }
    updateEnabled(); buildCrt(); repaint();
}
void VisualizerPanel::resized()
{
    const int w = getWidth();
    viewport = juce::Rectangle<float>(16, 48, (float)w - 32, (float)getHeight() - 272);
    const int top = getHeight() - 208, column = (w - 62) / 4;
    auto pair = [](juce::Label& label, juce::Component& control, int x, int y, int width)
    { label.setBounds(x, y, width, 20); control.setBounds(x, y + 22, width, 28); };
    pair(shapeLabel, shape, 16, top, column); pair(paletteLabel, palette, 26 + column, top, column);
    pair(sensitivityLabel, sensitivity, 36 + column * 2, top, column);
    pair(smoothingLabel, smoothing, 46 + column * 3, top, column);
    const int leftWidth = (w - 48) * 3 / 5, right = leftWidth + 32, rightWidth = w - right - 16;
    const int y = top + 68;
    rotationLabel.setBounds(26, y, 76, 28); rotation.setBounds(104, y, leftWidth - 236, 28);
    reset.setBounds(leftWidth - 122, y, 126, 28);
    crtLabel.setBounds(right + 10, y, 80, 28); crt.setBounds(right + 90, y, rightWidth - 102, 28);
    auto row = [](juce::Label& label, juce::Slider& control, int x, int lineY, int width)
    { label.setBounds(x, lineY, 62, 28); control.setBounds(x + 62, lineY, width - 62, 28); };
    row(angleLabel, angle, 26, y + 34, leftWidth - 20);
    row(tiltLabel, tilt, 26, y + 68, (leftWidth - 20) / 2);
    row(turnLabel, turn, 26 + (leftWidth - 20) / 2, y + 68, (leftWidth - 20) / 2);
    row(snowLabel, snow, right + 10, y + 34, rightWidth - 20);
    row(scanLabel, scan, right + 10, y + 68, rightWidth - 20);
}
juce::Point<float> VisualizerPanel::project(float x, float y, bool roundShape) const
{
    const float w = viewport.getWidth(), h = viewport.getHeight(), size = juce::jmin(w, h);
    const int r = (int)processor.value("rotation");
    float px = x * (roundShape ? size : w), py = y * (roundShape ? size : h);
    const float a = r > 0 ? -(processor.value("angle") + spin) * pi / 180 : 0;
    const float c = std::cos(a), s = std::sin(a);
    if (r == 2)
    {
        const float radius = roundShape ? size : std::sqrt(w * w + h * h);
        px /= radius; py /= radius;
        const float ax = (processor.value("tilt") + tiltSpin) * pi / 180;
        const float ay = (processor.value("turn") + turnSpin) * pi / 180;
        float z = std::sin(ax) * py; py *= std::cos(ax);
        const float nx = std::cos(ay) * px + std::sin(ay) * z;
        z = -std::sin(ay) * px + std::cos(ay) * z; px = nx;
        const float perspective = size * 0.94f / (1 - z / 4);
        px *= perspective; py *= perspective;
    }
    else if (!roundShape)
    {
        const float fit = juce::jmin(1.0f, juce::jmin(w / (std::abs(c) * w + std::abs(s) * h), h / (std::abs(s) * w + std::abs(c) * h)));
        px *= fit; py *= fit;
    }
    return { viewport.getCentreX() + (c * px - s * py) * 0.5f, viewport.getCentreY() - (s * px + c * py) * 0.5f };
}
void VisualizerPanel::drawVisualization(juce::Graphics& g)
{
    const int mode = (int)processor.value("shape"), color = juce::jlimit(0, 4, (int)processor.value("palette"));
    const float gain = processor.value("sensitivity") / 1.4f;
    g.setColour(primary[color]);
    juce::Path path;
    auto add = [&](juce::Point<float> point, bool first) { if (first) path.startNewSubPath(point); else path.lineTo(point); };
    if (mode == 8)
    {
        for (size_t i = 0; i < spectrum.size(); ++i)
        {
            const float left = -1 + (float)i / 32 + 0.004f, right = left + 0.0225f;
            const float top = -0.86f + juce::jmax(0.018f, spectrum[i] * 1.62f);
            juce::Path bar; bar.startNewSubPath(project(left, -0.86f, false));
            bar.lineTo(project(right, -0.86f, false)); bar.lineTo(project(right, top, false)); bar.lineTo(project(left, top, false)); bar.closeSubPath();
            g.setColour(primary[color]); g.fillPath(bar);
            g.setColour(accent[color]);
            const float peak = -0.86f + juce::jmax(0.025f, peaks[i] * 1.62f);
            g.drawLine({ project(left, peak, false), project(right, peak, false) }, 1.5f);
        }
        return;
    }
    if (mode == 9)
    {
        for (size_t i = 0; i < spectrum.size(); ++i)
        {
            const float a = tau * (float)i / 64, r = 0.36f + juce::jmax(0.015f, std::sqrt(spectrum[i]) * 0.5f);
            g.drawLine({ project(std::cos(a) * 0.36f, std::sin(a) * 0.36f, true), project(std::cos(a) * r, std::sin(a) * r, true) }, 2.5f);
        }
        for (int i = 0; i < 128; ++i) add(project(std::cos(tau * (float)i / 128) * 0.35f, std::sin(tau * (float)i / 128) * 0.35f, true), i == 0);
        path.closeSubPath(); g.setColour(accent[color]); g.strokePath(path, juce::PathStrokeType(1.5f)); return;
    }
    const int sides[] { 0, 0, 3, 4, 5, 6, 8, 10 };
    const int corners = sides[juce::jlimit(0, 7, mode)];
    const int count = corners == 0 ? 1024 : 1024 - 1024 % corners;
    for (int i = 0; i < count; ++i)
    {
        if (mode == 0) { add(project(-0.96f + 1.92f * (float)i / (float)(count - 1), juce::jlimit(-0.88f, 0.88f, waveform[(size_t)i] * gain * 1.6f), false), i == 0); continue; }
        const float t = (float)i / (float)count, a = tau * t + pi / 2;
        const float position = (t <= 0.5f ? t * 2 : 2 - t * 2) * 1023;
        const int index = (int)position;
        const float sample = waveform[(size_t)index] + (waveform[(size_t)juce::jmin(index + 1, 1023)] - waveform[(size_t)index]) * (position - (float)index);
        float radius = 0.55f;
        if (mode == 7)
        {
            const float sector = tau / 10, edgePosition = t * 10, phase = (edgePosition - std::floor(edgePosition)) * sector;
            const bool outer = (int)edgePosition % 2 == 0;
            const float start = outer ? 0.72f : 0.34f, end = outer ? 0.34f : 0.72f;
            radius = start * end * std::sin(sector) / (end * std::sin(sector - phase) + start * std::sin(phase));
        }
        else if (corners > 0)
        {
            const float sector = tau / (float)corners;
            const float phase = std::fmod(tau * t + (corners == 4 ? sector / 2 : 0), sector);
            radius = 0.68f * std::cos(pi / (float)corners) / std::cos(phase - sector / 2);
        }
        radius = juce::jlimit(0.12f, 0.88f, radius + sample * gain * 0.65f);
        add(project(std::cos(a) * radius, std::sin(a) * radius, true), i == 0);
    }
    if (mode != 0) path.closeSubPath();
    g.strokePath(path, juce::PathStrokeType(2.0f, juce::PathStrokeType::curved, juce::PathStrokeType::rounded));
}
void VisualizerPanel::buildCrt()
{
    const int mode = (int)processor.value("crt");
    if (mode == 0) { crtImage = {}; return; }
    const int w = juce::jmax(1, (int)viewport.getWidth() / 2), h = juce::jmax(1, (int)viewport.getHeight() / 2);
    if (crtImage.getWidth() != w || crtImage.getHeight() != h) crtImage = juce::Image(juce::Image::ARGB, w, h, true);
    const float snowAmount = mode == 1 || mode == 3 ? processor.value("snow") * 0.65f : 0;
    const float scanAmount = mode == 2 || mode == 3 ? processor.value("scan") : 0;
    juce::Image::BitmapData pixels(crtImage, juce::Image::BitmapData::writeOnly);
    for (int y = 0; y < h; ++y)
    {
        const float phase = ((1 - (float)y / (float)h) + (float)time * 0.18f) * 3;
        const float distance = std::abs(phase - std::floor(phase) - 0.5f) / 3;
        const float t = juce::jlimit(0.0f, 1.0f, (distance - 0.015f) / 0.075f);
        const float band = 1 - t * t * (3 - 2 * t);
        const float line = (float)((y + (int)(time * 60)) % 2);
        const float dim = scanAmount * (0.55f * line + 0.2f * band), glow = scanAmount * band * 0.11f;
        const float alpha = 1 - (1 - snowAmount) * (1 - dim) * (1 - glow);
        for (int x = 0; x < w; ++x)
        {
            const float grain = random.nextFloat() * snowAmount * (1 - dim) * (1 - glow);
            const float denominator = juce::jmax(0.00001f, alpha);
            pixels.setPixelColour(x, y, juce::Colour::fromFloatRGBA((grain + 0.45f * glow) / denominator, (grain + 0.65f * glow) / denominator, (grain + 0.72f * glow) / denominator, alpha));
        }
    }
}
void VisualizerPanel::paint(juce::Graphics& g)
{
    g.fillAll(juce::Colour(0xff111318));
    g.setColour(juce::Colours::white); g.setFont(20.0f); g.drawText("TRACK VISUALIZER", 18, 10, 280, 28, juce::Justification::centredLeft);
    g.setFont(13.0f); g.setColour(juce::Colour(0xffa4aebd));
    g.drawText("Track audio in / unchanged audio out", getWidth() - 300, 10, 282, 28, juce::Justification::centredRight);
    g.setColour(juce::Colour(0xff06080c)); g.fillRoundedRectangle(viewport, 6);
    {
        juce::Graphics::ScopedSaveState save(g); g.reduceClipRegion(viewport.toNearestInt());
        drawVisualization(g);
        if (crtImage.isValid() && (int)processor.value("crt") != 0)
        { g.setImageResamplingQuality(juce::Graphics::lowResamplingQuality); g.drawImage(crtImage, viewport); }
    }
    g.setColour(juce::Colour(0xff303744)); g.drawRoundedRectangle(viewport, 6, 1);
    const int top = getHeight() - 140, leftWidth = (getWidth() - 48) * 3 / 5, right = leftWidth + 32;
    g.setColour(juce::Colour(0xff191d25));
    g.fillRoundedRectangle(16, (float)top - 6, (float)leftWidth, 112, 6);
    g.fillRoundedRectangle((float)right, (float)top - 6, (float)getWidth() - right - 16, 112, 6);
    g.setColour(primary[juce::jlimit(0, 4, (int)processor.value("palette"))]);
    g.fillRoundedRectangle(18, (float)getHeight() - 16, level * 110, 4, 2);
    g.setColour(juce::Colour(0xff7e8797)); g.setFont(11.0f);
    g.drawText("Settings and automation are saved by your DAW", 146, getHeight() - 24, getWidth() - 164, 18, juce::Justification::centredRight);
}

class VisualizerFloatingWindow final : public juce::DocumentWindow
{
public:
    explicit VisualizerFloatingWindow(VisualizerProcessor& p)
        : DocumentWindow(p.windowTitle(), juce::Colour(0xff111318), DocumentWindow::allButtons)
    {
        setUsingNativeTitleBar(true);
        setContentOwned(new VisualizerPanel(p), true);
        setResizable(true, true);
        setResizeLimits(780, 550, 1800, 1230);
        centreWithSize(1000, 710);
        setVisible(true);
    }
    void closeButtonPressed() override { setVisible(false); }
};
void VisualizerProcessor::openVisualizerWindow()
{
    if (floatingWindow == nullptr) floatingWindow = std::make_unique<VisualizerFloatingWindow>(*this);
    floatingWindow->setVisible(true); floatingWindow->setMinimised(false); floatingWindow->toFront(true);
}
VisualizerEditor::VisualizerEditor(VisualizerProcessor& p) : AudioProcessorEditor(p), processor(p)
{
    addAndMakeVisible(open); open.onClick = [this] { processor.openVisualizerWindow(); };
    setSize(440, 140);
    // Owned by the processor so other tracks can open editors without closing this view.
    processor.openVisualizerWindow();
}
VisualizerEditor::~VisualizerEditor() = default;
void VisualizerEditor::resized() { open.setBounds(20, 86, getWidth() - 40, 32); }
void VisualizerEditor::paint(juce::Graphics& g)
{
    g.fillAll(juce::Colour(0xff111318)); g.setColour(juce::Colours::white); g.setFont(19.0f);
    g.drawText(processor.windowTitle(), 20, 12, getWidth() - 40, 28, juce::Justification::centredLeft);
    g.setColour(juce::Colour(0xffa4aebd)); g.setFont(13.0f);
    g.drawText("One independent, resizable window for this track instance.", 20, 46, getWidth() - 40, 26, juce::Justification::centredLeft);
}