import re

with open("Assets/Editor/SceneBuilder.cs", "r") as f:
    code = f.read()

# Scale preferred heights
code = code.replace('"SecRouteName", "ENTER ROUTE NAME", 150', '"SecRouteName", "ENTER ROUTE NAME", 400')
code = code.replace('"SecSelectMap", "SELECT MAP", 180', '"SecSelectMap", "SELECT MAP", 480')
code = code.replace('"SecOpMode", "OPERATION MODE", 300', '"SecOpMode", "OPERATION MODE", 800')
code = code.replace('"SecInclination", "INCLINATION METERS", 200', '"SecInclination", "INCLINATION METERS", 530')

# Scale font sizes in Label() calls
def scale_label(m):
    # m.group(1) is before size, m.group(2) is size, m.group(3) is after
    size = int(m.group(2))
    new_size = int(size * 2.666)
    return f"Label({m.group(1)}, {new_size}, {m.group(3)})"

code = re.sub(r'Label\((.*?),\s*(\d+),\s*(.*?)\)', scale_label, code)

# Scale font sizes in Btn() calls
def scale_btn(m):
    size = int(m.group(2))
    new_size = int(size * 2.666)
    return f"Btn({m.group(1)}, {new_size})"

code = re.sub(r'Btn\((.*?),\s*(\d+)\)', scale_btn, code)

# Scale TextMeshPro font sizes
def scale_tmp(m):
    size = int(m.group(1))
    new_size = int(size * 2.666)
    return f"fontSize = {new_size};"

code = re.sub(r'fontSize = (\d+);', scale_tmp, code)

# Scale CANVAS_RES
code = code.replace('new Vector2(960, 540)', 'new Vector2(2560, 1440)')
# Fix GPS resolution mapBrowser.Init
code = code.replace('new Vector2Int(960, 540)', 'new Vector2Int(2560, 1440)')

with open("Assets/Editor/SceneBuilder.cs", "w") as f:
    f.write(code)
print("Scaled UI")
