<template>
    <div :enable="hasInstructions"
        trigger="hover-focus"
        append-to="body"><!-- v-bind:style="{ width: getWidth() + 'px!important' }"-->
        <div class="ag-cell-label-container"
            style="flex-direction:row; justify-content: flex-start;"
            v-if="question.entityType =='CategoricalSingle' || question.entityType =='CategoricalMulti'">
            <div class="ag-header-cell-label cell-bordered"
                v-for="option in getOptions"
                :key="question.id + '_' + option.value"
                style="width:180px!important;max-width:180px;" >
                <span>{{option.title}}</span>
            </div>
        </div>
    </div>
</template>

<script lang="js">

export default {
    name: 'MatrixRoster_QuestionTitle',

    data() {
        return {
            title: null,
            instruction: null,
            hasInstructions: false,
            question: null,
        }
    },
    computed: {
        getOptions(){
            return this.question.options
        },
    },
    methods: {
    },
    created() {
        this.title = this.params.title
        this.instruction = this.params.instruction
        //this.hasInstructions = this.instruction != undefined && this.instruction != null && this.instruction != ''
        this.question = this.params.question
    },
}
</script>
