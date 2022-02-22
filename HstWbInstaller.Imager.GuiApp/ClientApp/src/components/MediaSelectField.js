import React from "react";
import {isNil} from "lodash";
import SelectField from "./SelectField";

const initialState = {
    medias: null,
    session: null
}

export default function MediaSelectField(props) {
    const {
        id,
        label,
        path,
        onChange,
        session
    } = props
    
    const [state, setState] = React.useState({...initialState})

    const handleGetMedias = React.useCallback(() => {
        async function getMedias() {
            const response = await fetch('list');
            const data = await response.json();
            setState({
                ...state,
                medias: data,
                session: session
            })
        }

        getMedias()
    }, [setState, state, session])

    React.useEffect(() => {
        if (session === state.session) {
            return
        }
        handleGetMedias()
    }, [session, state.medias, state.session, handleGetMedias])

    const {
        medias,
    } = state
    
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
            value={path || ''}
            options={(medias || []).map((media) => {
                return {
                    title: media.model,
                    value: media.path
                }
            })}
            onChange={(value) => handleChange(value)}
        />
    )
}