import React from "react";
import {isNil} from "lodash";
import SelectField from "./SelectField";
import {formatBytes} from "../utils/Format";

export default function MediaSelectField(props) {
    const {
        id,
        label,
        path,
        medias,
        onChange,
    } = props
    
    const handleChange = (path) => {
        if (isNil(onChange)) {
            return
        }
        const media = (medias || []).find(media => media.path === path)
        onChange(media)
    }
    
    return (
        <SelectField
            label={label}
            id={id}
            emptyLabel="None available"
            value={path || ''}
            options={(medias || []).map((media) => {
                return {
                    title: `${media.name} (${formatBytes(media.diskSize)})`,
                    value: media.path
                }
            })}
            onChange={(value) => handleChange(value)}
        />
    )
}