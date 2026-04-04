import { Tool } from '@opencode/ai';
import { z } from 'zod';
import { pathToFileURL } from 'url';
import { fileURLToPath } from 'url';
import * as LSP from '../lsp/index.js';

export const AsmProvenStatesTool = Tool.define(
	'asm-proven-states',
	{
		description: 'Get Z3-proven register/flag states for assembly code. Use this to derive register values before and after instruction execution.',
		parameters: z.object({
			filePath: z.string().describe('The absolute or relative path to the assembly file'),
			lineRange: z.array(z.number()).optional().describe('Optional [startLine, endLine] inclusive range. If not provided, returns all lines.'),
		}),
		execute: async (args) => {
			const uri = pathToFileURL(args.filePath).href;
			const client = await LSP.getClients(args.filePath);
			
			if (client.length === 0) {
				return { error: 'No LSP server found for this file' };
			}

			const result = await client[0].connection.sendRequest('asm/getProvenStates', {
				uri,
				lineRange: args.lineRange as [number, number] | undefined,
			});

			// Format the proven states for the LLM
			if (!result || result.States.length === 0) {
				return 'No proven states available for this file.';
			}

			let output = 'Z3-proven register/flag states:\n\n';
			for (const state of result.States) {
				output += `Line ${state.Line}:\n`;
				if (state.BeforeState) {
					output += `  Before: ${state.BeforeState}\n`;
				}
				if (state.AfterState) {
					output += `  After:  ${state.AfterState}\n`;
				}
				output += `  ProvenBy: ${state.ProvenBy} (${state.Confidence})\n\n`;
			}

			return output;
		},
	},
);
