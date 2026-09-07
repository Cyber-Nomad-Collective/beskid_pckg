import {
	layoutAstTree,
	type AstGraphModel,
} from "@beskid/ui-react/graph";

const model: AstGraphModel = {
	roots: ["root"],
	nodes: [{ id: "root", kind: "Root", label: "Root" }],
};

const layout = layoutAstTree(model);
const firstNodeX: number | undefined = layout.nodes[0]?.position.x;

void firstNodeX;
