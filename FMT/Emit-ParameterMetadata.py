"""Run an unmodified upstream parser, adding source provenance to XML nodes only."""
import runpy
import sys
from pathlib import Path

parser = Path(sys.argv[1]).resolve()
source_root = parser.parents[3]
sys.path.insert(0, str(parser.parent))
sys.argv = [str(parser)] + sys.argv[2:]
import xmlemit

original_emit = xmlemit.XmlEmit.emit

def emit_with_source(self, group):
    original_emit(self, group)
    nodes = list(self.current_element[-1])
    predicate = getattr(self, 'should_emit_param', lambda p: True)
    emitted = [p for p in group.params if predicate(p)]
    assert len(nodes) == len(emitted)
    for node, param in zip(nodes, emitted):
        assert node.get('name') == param.name
        origin = Path(param.real_path)
        node.set('source', (origin.relative_to(source_root) if origin.is_absolute() else origin).as_posix())

xmlemit.XmlEmit.emit = emit_with_source
runpy.run_path(str(parser), run_name='__main__')
