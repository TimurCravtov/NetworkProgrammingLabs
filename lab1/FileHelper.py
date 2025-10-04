import sys

def file_has_one_of_extensions(file_path: str, allowed_extensions: list) -> bool:
    file_path_lower = file_path.lower()
    return any(file_path_lower.endswith(ext) for ext in allowed_extensions)

def parse_args():
    args = {}
    key = None
    for arg in sys.argv[1:]:  # skip script name
        if arg.startswith("--"):
            key = arg.lstrip("-")
            args[key] = True  # default True if no value
        else:
            if key:
                args[key] = arg
                key = None
    return args