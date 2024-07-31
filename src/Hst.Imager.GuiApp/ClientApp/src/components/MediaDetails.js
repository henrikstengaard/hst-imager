import {formatBytes} from "../utils/Format";
import {set} from "lodash";
import React from "react";
import {createMasterBootRecordParts} from "./MbrDetails";
import {createGuidPartitionTableParts} from "./GptDetails";
import {createRigidDiskBlockParts} from "./RdbDetails";
import Part from "./Part";
import Accordion from "./Accordion";
import CheckboxField from "./CheckboxField";

export default function MediaDetails(props) {
    const {
        media
    } = props

    const [state, setState] = React.useState({
        humanReadable: true
    });
    
    const { humanReadable } = state

    const handleChange = ({ name, value }) => {
        set(state, name, value)
        setState({...state})
    }
    
    const diskPart = {
        type: 'list',
        title: `Disk: ${media.name}, ${formatBytes(media.diskSize)}`,
        fields: [{
            label: 'Name',
            value: media.name
        }, {
            label: 'Path',
            value: media.path
        }, {
            label: 'Size',
            value: humanReadable ? formatBytes(media.diskSize) : media.diskSize
        }],
        columns: [{
            name: 'Name',
            align: 'left',
        },{
            name: 'Path',
            align: 'left',
        },{
            name: 'Size',
            align: 'left',
        }],
        rows: [{
            values: [
                media.name,
                media.path,
                humanReadable ? formatBytes(media.diskSize) : media.diskSize
            ]
        }]
    }

    const parts = [diskPart]
        .concat(createMasterBootRecordParts({media, humanReadable}))
        .concat(createGuidPartitionTableParts({media, humanReadable}))
        .concat(createRigidDiskBlockParts({media, humanReadable}))
    
    return (
        <React.Fragment>
            <CheckboxField
                id="human-readable"
                label="Show human readable sizes"
                value={humanReadable}
                onChange={(checked) => handleChange({ name: 'humanReadable', value: checked})}
            />
            {parts.map((part, index) => {
                return (
                    <Accordion title={part.title} key={index}>
                        <Part part={part} humanReadable={humanReadable}/>
                    </Accordion>
                )}
            )}
        </React.Fragment>
    )
}